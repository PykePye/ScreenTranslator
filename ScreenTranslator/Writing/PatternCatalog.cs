namespace ScreenTranslator.Writing;

/// <summary>One entry of the closed mistake taxonomy.</summary>
/// <param name="Id">Stable snake_case identifier stored in mistake_history.md.</param>
/// <param name="Group">1 = serious grammar, 2 = word class, 3 = word choice. Owned by the app, never by the model.</param>
/// <param name="Hint">Short English gloss shown to the model so it can pick the right id.</param>
public sealed record PatternDefinition(string Id, int Group, string Hint);

/// <summary>
/// Closed list of mistake patterns the writing assistant may report.
/// Earlier versions let the model invent a fresh snake_case name on every call, so the same
/// mistake was never counted twice and the repeated-mistake reminder could never fire. The model
/// now picks from this list, and the group is resolved here rather than by the model.
/// </summary>
public static class PatternCatalog
{
    public const string Fallback = "other";

    public static readonly IReadOnlyList<PatternDefinition> All =
    [
        new("vi_article_missing",         1, "a/an/the omitted before a noun that needs one"),
        new("vi_article_extra",           1, "an article used where English takes none"),
        new("vi_plural_after_quantifier", 1, "singular noun after all/many/some/several/both"),
        new("vi_plural_general_noun",     1, "singular noun where English uses a general plural"),
        new("vi_countable_uncountable",   1, "uncountable noun treated as countable or vice versa"),
        new("vi_word_order",              1, "Vietnamese word order carried into English"),
        new("vi_verb_tense",              1, "wrong or inconsistent tense"),
        new("vi_subject_verb_agreement",  1, "subject and verb do not agree in number"),
        new("vi_pronoun_agreement",       1, "pronoun does not match the noun it replaces"),
        new("vi_passive_voice_missing",   1, "active voice where the subject does not perform the action"),
        new("vi_preposition_wrong",       1, "wrong preposition"),
        new("vi_preposition_extra",       1, "preposition added after a verb that takes a direct object"),
        new("vi_missing_subject_or_verb", 1, "clause missing its subject or its main verb"),
        new("vi_run_on_sentence",         1, "independent clauses joined without punctuation or conjunction"),
        new("vi_capitalization",          1, "capitalization of I, proper nouns or place names"),
        new("vi_compound_adjective",      1, "compound modifier missing its hyphen or article"),
        new("vi_word_class",              2, "adjective/adverb/noun/verb form used in the wrong slot"),
        new("vi_word_choice_informal",    3, "wording too casual for a work message"),
        new("vi_word_choice_vague",       3, "vague wording that leaves the reader guessing"),
        new("vi_collocation",             3, "grammatical but not what a native speaker would say"),
        new(Fallback,                     3, "a genuine issue that fits none of the ids above")
    ];

    /// <summary>
    /// Free-form ids written by earlier versions of the app, mapped onto the closed list so the
    /// existing mistake_history.md keeps counting toward the repeat threshold.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["article_omission_with_countable_noun"] = "vi_article_missing",
        ["missing_definite_article"] = "vi_article_missing",
        ["noun_pluralization_after_quantifier"] = "vi_plural_after_quantifier",
        ["plural_noun_agreement"] = "vi_plural_after_quantifier",
        ["noun_pluralization_for_general_meaning"] = "vi_plural_general_noun",
        ["noun_singular_plural_for_general_outcome"] = "vi_plural_general_noun",
        ["preposition_error_with_transitive_verb"] = "vi_preposition_extra",
        ["sentence_fragment_preposition_article"] = "vi_missing_subject_or_verb",
        ["adjective_for_adverb"] = "vi_word_class",
        ["adverb_form_choice"] = "vi_word_class",
        ["adverb_placement"] = "vi_word_order",
        ["awkward_sentence_structure"] = "vi_word_order",
        ["subject_noun_agreement_preposition_for_location"] = "vi_word_order",
        ["unclear_technical_phrasing"] = "vi_word_choice_vague",
        ["vague_unclear_phrase"] = "vi_word_choice_vague",
        ["vague_pronoun_reference"] = "vi_word_choice_vague",
        ["awkward_phrasing"] = "vi_collocation",
        ["collocation_singular_plural_verb_choice"] = "vi_collocation",
        ["informal_verb_choice"] = "vi_word_choice_informal",
        ["pronoun_capitalization"] = "vi_capitalization",
        ["proper_noun_capitalization"] = "vi_capitalization",
        ["run_on_sentence"] = "vi_run_on_sentence",
        ["pronoun_number_agreement"] = "vi_pronoun_agreement",
        ["active_passive_voice_confusion"] = "vi_passive_voice_missing",
        ["compound_adjective_formation"] = "vi_compound_adjective"
    };

    /// <summary>Keyword probes for ids that are neither known nor aliased, tried in order.</summary>
    private static readonly (string Keyword, string Id)[] Probes =
    [
        ("capitaliz", "vi_capitalization"),
        ("run_on", "vi_run_on_sentence"),
        ("article", "vi_article_missing"),
        ("quantifier", "vi_plural_after_quantifier"),
        ("plural", "vi_plural_general_noun"),
        ("singular", "vi_plural_general_noun"),
        ("uncountable", "vi_countable_uncountable"),
        ("passive", "vi_passive_voice_missing"),
        ("subject_verb", "vi_subject_verb_agreement"),
        ("preposition", "vi_preposition_wrong"),
        ("pronoun", "vi_pronoun_agreement"),
        ("tense", "vi_verb_tense"),
        ("fragment", "vi_missing_subject_or_verb"),
        ("word_order", "vi_word_order"),
        ("awkward", "vi_word_order"),
        ("adverb", "vi_word_class"),
        ("adjective", "vi_word_class"),
        ("word_class", "vi_word_class"),
        ("hyphen", "vi_compound_adjective"),
        ("compound", "vi_compound_adjective"),
        ("vague", "vi_word_choice_vague"),
        ("unclear", "vi_word_choice_vague"),
        ("informal", "vi_word_choice_informal"),
        ("formal", "vi_word_choice_informal"),
        ("collocation", "vi_collocation"),
        ("phrasing", "vi_collocation")
    ];

    private static readonly Dictionary<string, PatternDefinition> ById =
        All.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps any incoming pattern string onto a catalog id. Never throws.</summary>
    public static string Normalize(string? raw)
    {
        var slug = Slugify(raw);
        if (slug.Length == 0) return Fallback;
        if (ById.ContainsKey(slug)) return slug;
        if (Aliases.TryGetValue(slug, out var aliased)) return aliased;

        foreach (var (keyword, id) in Probes)
            if (slug.Contains(keyword, StringComparison.Ordinal)) return id;

        return Fallback;
    }

    /// <summary>Group for a catalog id. Resolved here so one mistake never changes group between calls.</summary>
    public static int GroupOf(string id) =>
        ById.TryGetValue(id, out var definition) ? definition.Group : ById[Fallback].Group;

    public static string GroupName(int group) => group switch
    {
        1 => "1 - Serious grammar",
        2 => "2 - Word class",
        _ => "3 - Word choice"
    };

    /// <summary>Renders the catalog as the allow-list block injected into the prompt.</summary>
    public static string BuildPromptBlock() =>
        string.Join("\n", All.Select(p => $"- {p.Id} — {p.Hint}"));

    private static string Slugify(string? raw) =>
        System.Text.RegularExpressions.Regex
            .Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), "[^a-z0-9]+", "_")
            .Trim('_');
}
