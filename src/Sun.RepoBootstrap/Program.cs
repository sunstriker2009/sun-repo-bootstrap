using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var mode = Arg("--mode") ?? "audit";
var repo = Arg("--repo") ?? throw new ArgumentException("--repo owner/name required");
var checks = (Arg("--checks") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var confirmation = Arg("--confirm") ?? "";
if (!new[] { "audit", "plan", "apply", "verify" }.Contains(mode)) throw new ArgumentException("Unsupported mode");
if (!System.Text.RegularExpressions.Regex.IsMatch(repo, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new ArgumentException("Invalid repo");
var token = Environment.GetEnvironmentVariable("GH_TOKEN") ?? throw new InvalidOperationException("GH_TOKEN required (fine-grained token with repository Administration write for apply; read for audit)");
using var http = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
http.DefaultRequestHeaders.UserAgent.ParseAdd("sun-repo-bootstrap/0.1");
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

async Task<JsonNode?> Api(string method, string path, object? payload = null)
{
    using var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (payload is not null) request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    using var response = await http.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub {method} {path}: {(int)response.StatusCode} {body}", null, response.StatusCode);
    return string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
}

var info = await Api("GET", $"repos/{repo}") ?? throw new InvalidOperationException("Repository not found");
var main = info["default_branch"]?.GetValue<string>() ?? "main";
var settings = new Dictionary<string, object> {
    ["allow_squash_merge"] = true, ["allow_merge_commit"] = false, ["allow_rebase_merge"] = false,
    ["allow_auto_merge"] = true, ["delete_branch_on_merge"] = true,
    ["squash_merge_commit_title"] = "PR_TITLE", ["squash_merge_commit_message"] = "PR_BODY"
};
var diffs = settings.Where(kv => info[kv.Key]?.ToJsonString() != JsonSerializer.Serialize(kv.Value))
    .Select(kv => $"{kv.Key}: {info[kv.Key]?.ToJsonString() ?? "(unknown)"} -> {JsonSerializer.Serialize(kv.Value)}").ToArray();
var rules = await Api("GET", $"repos/{repo}/rulesets?includes_parents=false") as JsonArray ?? new JsonArray();
var existing = rules.FirstOrDefault(x => x?["name"]?.GetValue<string>() == "SUN Main Protection");
var rulePresent = existing is not null;
Console.WriteLine($"SUN Git Policy v1.0 | {repo} | default branch: {main} | mode: {mode}");
foreach (var diff in diffs) Console.WriteLine($"SETTING DRIFT: {diff}");
Console.WriteLine(rulePresent ? "RULESET: SUN Main Protection exists (contents must be verified)" : "RULESET MISSING: SUN Main Protection");
if (checks.Length == 0) Console.WriteLine("WARNING: no required CI checks specified. Applying protection is blocked until explicit check names are supplied.");
if (mode == "plan" || mode == "audit") { Environment.ExitCode = mode == "audit" && (diffs.Length > 0 || !rulePresent) ? 2 : 0; return; }
if (mode == "apply")
{
    if (confirmation != repo) throw new InvalidOperationException("Apply requires --confirm equal to target repository");
    if (checks.Length == 0) throw new InvalidOperationException("Apply requires --checks with exact CI check names");
    if (main != "main") throw new InvalidOperationException("Expected default branch main; review repository before applying");
    // Do not overwrite existing rulesets: human review required to avoid weakening established controls.
    if (rulePresent) throw new InvalidOperationException("SUN Main Protection already exists. Refusing to overwrite: audit existing ruleset manually before applying.");
    // GitHub's check names must already exist; validate against check runs for latest main commit.
    var sha = info["default_branch"] is null ? null : (await Api("GET", $"repos/{repo}/branches/{main}"))?["commit"]?["sha"]?.GetValue<string>();
    var runs = await Api("GET", $"repos/{repo}/commits/{sha}/check-runs?per_page=100");
    var known = (runs?["check_runs"] as JsonArray ?? new JsonArray()).Select(x => x?["name"]?.GetValue<string>()).Where(x => x is not null).ToHashSet();
    var missing = checks.Where(x => !known.Contains(x)).ToArray();
    if (missing.Length > 0) throw new InvalidOperationException("Check names not found on latest main commit: " + string.Join(", ", missing) + ". Run CI on main first.");
    // Ruleset created before modifying merge settings, so failure cannot leave merge-method changes without branch protection.
    var ruleset = new {
        name = "SUN Main Protection", target = "branch", enforcement = "active",
        conditions = new { ref_name = new { include = new[] { "refs/heads/main" }, exclude = Array.Empty<string>() } },
        bypass_actors = Array.Empty<object>(),
        rules = new object[] {
            new { type = "deletion" }, new { type = "non_fast_forward" },
            new { type = "pull_request", parameters = new { required_approving_review_count = 0, dismiss_stale_reviews_on_push = true, require_code_owner_review = false, require_last_push_approval = false, required_review_thread_resolution = true } },
            new { type = "required_status_checks", parameters = new { strict_required_status_checks_policy = true, do_not_enforce_on_create = false, required_status_checks = checks.Select(x => new { context = x }).ToArray() } }
        }
    };
    await Api("POST", $"repos/{repo}/rulesets", ruleset);
    Console.WriteLine("Created active SUN Main Protection ruleset.");
    await Api("PATCH", $"repos/{repo}", settings);
    Console.WriteLine("Applied merge and branch-cleanup settings.");
}
var updated = await Api("GET", $"repos/{repo}");
var remaining = settings.Where(kv => updated?[kv.Key]?.ToJsonString() != JsonSerializer.Serialize(kv.Value)).Select(kv => kv.Key).ToArray();
var activeRules = await Api("GET", $"repos/{repo}/rulesets?includes_parents=false") as JsonArray ?? new JsonArray();
var active = activeRules.FirstOrDefault(x => x?["name"]?.GetValue<string>() == "SUN Main Protection" && x?["enforcement"]?.GetValue<string>() == "active");
if (active is not null)
{
    var detail = await Api("GET", $"repos/{repo}/rulesets/{active["id"]}");
    var rs = detail?["rules"] as JsonArray ?? new JsonArray();
    var required = rs.FirstOrDefault(x => x?["type"]?.GetValue<string>() == "required_status_checks");
    var pr = rs.FirstOrDefault(x => x?["type"]?.GetValue<string>() == "pull_request");
    if (required?["parameters"]?["strict_required_status_checks_policy"]?.GetValue<bool>() != true ||
        pr?["parameters"]?["required_review_thread_resolution"]?.GetValue<bool>() != true ||
        (required?["parameters"]?["required_status_checks"] as JsonArray)?.Count == 0)
        throw new InvalidOperationException("Ruleset exists but strict CI or conversation resolution is not enforced");
}
if (remaining.Length > 0 || active is null) throw new InvalidOperationException("Policy verification failed: " + string.Join(",", remaining));
Console.WriteLine("Verified: merge settings and active ruleset with strict CI and resolved conversations.");
Console.WriteLine("MANUAL GATE: sensitive changes still require enforceable reviewer/CODEOWNERS policy and a failing-CI PR test.");
string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
