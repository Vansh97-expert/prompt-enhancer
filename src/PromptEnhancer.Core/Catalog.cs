using System.Text.RegularExpressions;

namespace PromptEnhancer.Core;

public sealed record Technique(string Id, string Label, string Instruction)
{
    public override string ToString() => Label;
}

public sealed record TaskRoute(string Id, string Label, string Signals, Technique[] Techniques)
{
    public override string ToString() => Label;
}

// The first technique is the default. Each alternative is usable in a copyable chat prompt.
public static class Catalog
{
    private static Technique T(string id, string label, string text) => new(id, label, text);

    public static readonly TaskRoute[] Routes =
    [
        new("code_change", "Coding and debugging", @"\b(code|coding|debug|debugging|bug|feature|function|implement|endpoint|refactor|crash|stack trace|exception|unit test|vibe cod\w*)\b|\b(add|fix|build|create|share|delete)\b.{0,45}\b(app|button|project|api|login|website|component)\b",
            [T("direct","Behavior and acceptance checks","Describe the intended behavior, preserve existing behavior outside this change, and include a focused verification check."),
             T("chaining","Inspect, change, then verify","Identify the affected code, make the smallest change that meets the request, and distinguish checks actually run from suggested checks.")]),
        new("code_review","Defensive code review",@"\b(code review|review (the |this |my )?(code|patch|diff)|audit|security review|vulnerabilit\w*|pull request review)\b",
            [T("direct","Evidence-based review","For each supported finding, identify its location, impact, and a defensive correction. Say when the supplied code supports no findings."),
             T("structured","Finding-by-finding review","Organize confirmed findings by location, condition, impact, and remediation.")]),
        new("analysis","Analysis and comparison",@"\b(analy[sz]e|analysis|compar(e|ison)|contrast|trade.?offs?|versus|pros and cons)\b",
            [T("direct","Compare on shared criteria","Use comparable criteria and units. Identify assumptions that affect the conclusion."),
             T("grounded","Source-grounded comparison","Support comparisons with the available evidence and flag missing evidence.")]),
        new("writing","Drafting and editing",@"\b(write|writ(e|ing)|draft|rewrite|edit|email|letter|post|message|proofread|polish)\b",
            [T("audience_brief","Audience and voice","Match the requested audience, purpose, and tone. Preserve the author's facts and meaning."),
             T("direct","Direct writing brief","Produce the requested text and preserve the stated facts, wording constraints, and editing scope.")]),
        new("extraction","Information extraction",@"\b(extract|pull out|pick out|list (the )?(fields|names|dates)|extracting)\b",
            [T("structured","Field-by-field extraction","Extract the requested fields from the supplied material, preserving associations and marking missing values."),
             T("grounded","Trace values to evidence","Use only supported values and retain their connection to the source.")]),
        new("classification","Classification and intent routing",@"\b(classif\w*|categori[sz]e|label (these|each|the)|route (these|each|the)|sort into)\b",
            [T("direct","Use the requested labels","Apply the supplied label meanings consistently and flag items with insufficient evidence."),
             T("structured","One decision per item","Keep each item paired with its assigned label. Respect the permitted label set.")]),
        new("planning","Planning and scheduling",@"\b(plan|schedule|itinerary|roadmap|milestones?|trip)\b",
            [T("direct","Milestones and dependencies","Arrange the work around the stated resources, deadlines, and dependencies."),
             T("least_to_most","Dependency-first plan","Start with prerequisites and order later steps around the work they depend on.")]),
        new("reasoning","Math, logic, and problem solving",@"\b(solve|equation|math|calculate|proof|prove|probability|algebra|integral)\b",
            [T("direct","Givens and checkable result","Use the given values and units. Provide a checkable result in the requested form."),
             T("verification","Independent check","Verify the conclusion against the original givens, units, and constraints.")]),
        new("summarization","Summarization and synthesis",@"\b(summari[sz]e|summary|sum up|condense|synthesi[sz]e|tldr)\b",
            [T("grounded","Faithful summary","Use the supplied material. Preserve important caveats, attribution, ordering, and length requirements."),
             T("direct","Concise synthesis","Condense the source around the requested priorities while preserving its meaning.")]),
        new("research_qa","Document questions and research",@"\b(research|look up|cite|citations|according to|based on (the|this) (pdf|document|paper)|latest|answer.{0,24}(document|article|paper))\b",
            [T("grounded","Evidence and gaps","Answer within the permitted source scope. Identify supporting evidence and acknowledge gaps."),
             T("direct","Focused research question","Answer the specific question and distinguish supported conclusions from uncertainty.")]),
        new("transformation","Translation and format conversion",@"\b(translat\w*|convert|reformat|locali[sz]e)\b",
            [T("direct","Preserve meaning and identifiers","Return the requested conversion while preserving meaning, identifiers, placeholders, and requested formatting."),
             T("structured","Respect the target format","Map the supplied content to the requested format and preserve its values exactly where required.")]),
        new("ideation","Ideation and design options",@"\b(brainstorm|ideas?|ideation|design options|suggest names)\b",
            [T("audience_brief","Options for the audience","Generate distinct feasible options that fit the intended audience and constraints."),
             T("direct","Distinct alternatives","Keep alternatives meaningfully different while respecting the requested count and scope.")]),
        new("tutoring","Tutoring and explanation",@"\b(teach|explain|tutor|help me understand|lesson|learn)\b",
            [T("audience_brief","Explain at the learner’s level","Match the explanation to the learner and preserve any request for hints or a worked answer."),
             T("direct","Focused explanation","Explain the requested concept and use an example only when it fits the requested output.")]),
        new("media_analysis","Media interpretation",@"\b(image|photo|video|audio|screenshot|picture|recording)\b",
            [T("multimodal","Observations from the asset","Base observations on the referenced media and identify any unavailable evidence."),
             T("grounded","Evidence in the media","Tie observations to visible or audible details. Distinguish observations from inferences.")]),
        new("evaluation","Evaluation and grading",@"\b(grade|grading|score|evaluate|rubric|assess)\b",
            [T("structured","Criterion-by-criterion verdict","Apply the requested criteria consistently and support each judgment with evidence."),
             T("direct","Verdict and evidence","Give the requested assessment using only the stated standards and available evidence.")]),
        new("action","Authorized operational action",@"\b(send|book|purchase|cancel|submit|deploy|publish|reserve|schedule a meeting)\b",
            [T("direct","Action and prerequisites","Keep the requested action, destination, and permission scope explicit. Report completion only when verified."),
             T("structured","Action checklist","Identify prerequisites and the intended outcome within the user's authorization.")])
    ];

    public static readonly TaskRoute General = new("general", "General request", "",
        [T("direct","Direct request","Fulfil the stated request while preserving its scope and constraints.")]);
    public static IEnumerable<TaskRoute> Choices => Routes.Append(General);

    public static Technique Recommend(TaskRoute route, string instructions)
    {
        var alternative = route.Techniques.Last();
        var trigger = alternative.Id switch
        {
            "chaining" => @"\b(reproduce|inspect|diagnose)\b.*\b(fix|patch|test)\b",
            "grounded" => @"\b(sources?|evidence|according to|cite)\b",
            "structured" => @"\b(json|csv|table|fields|checklist)\b",
            "least_to_most" => @"\b(dependencies|prerequisites)\b",
            "verification" => @"\b(verify|double.check|check my)\b",
            _ => "(?!)"
        };
        return Regex.IsMatch(instructions,trigger,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(250))
            ? alternative : route.Techniques[0];
    }

    public static TaskRoute[] Detect(string instructions)
    {
        // Normalize common informal forms for routing only. The draft retains its exact spelling.
        var normalized = instructions.ToLowerInvariant()
            .Replace("summrize","summarize").Replace("summerize","summarize").Replace("sumarize","summarize")
            .Replace("buld","build").Replace("codign","coding").Replace("plz","please")
            .Replace("rite an","write an").Replace("explane","explain").Replace("comapre","compare");
        var hits = Routes.Select(route => (Route: route, Hit: Regex.Match(normalized, route.Signals,
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))))
            .Where(x => x.Hit.Success)
            .OrderBy(x => x.Hit.Index)
            .ThenByDescending(x => x.Hit.Length)
            .Select(x => x.Route).ToList();
        // Generic writing verbs often introduce a more specific requested artifact.
        if (hits.Count > 1 && hits[0].Id == "writing" &&
            Regex.IsMatch(normalized, @"\b(write|draft|rewrite|edit)\s+(?:me\s+)?(?:an?\s+|the\s+|this\s+)?(?:function|code|component|unit test|summary|plan|schedule|proof|translation)\b"))
            hits.RemoveAt(0);
        // A file or image is often an input to a different deliverable.
        if (hits.Count > 1 && hits[0].Id == "media_analysis")
        {
            var media = hits[0];
            hits.RemoveAt(0);
            hits.Add(media);
        }
        return hits.Count == 0 ? [General] : hits.ToArray();
    }
}

