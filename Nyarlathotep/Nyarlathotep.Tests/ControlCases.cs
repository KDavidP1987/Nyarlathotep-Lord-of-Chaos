namespace Nyarlathotep.Tests;

/// <summary>One control of a plan (event-library D31, walkable-spawns D9): the plan's slug and the D-item it belongs to
/// (its key, "walkable-spawns D2"), its name, and its failing, passing and empty cases. A test row names test methods of <see cref="Class"/> (`&lt;Name&gt;_fails_when_…`, `&lt;Name&gt;_passes_…`,
/// `&lt;Name&gt;_empty_…`); a cmd row names the script check or selftest in <see cref="Class"/> and its fixture paths
/// (under tools/) or selftest case names.</summary>
public sealed record ControlRow(string Control, string Name, string Kind, string Class, string[] Bad, string[] Good, string[] Empty,
    string Plan = ControlCases.EventLibrary)
{
    /// <summary>The row's key: plan slug and D-id.</summary>
    public string Key => $"{Plan} {Control}";
}

/// <summary>The authoritative list of the plans' control cases (event-library D31, walkable-spawns D9), compared with
/// each listed plan's own D-items by ControlCaseTests.</summary>
public static class ControlCases
{
    public const string EventLibrary = "event-library";
    public const string WalkableSpawns = "walkable-spawns";

    /// <summary>The plans whose controls the table lists; each is copied to the test output under Resources/.</summary>
    public static readonly string[] Plans = [EventLibrary, WalkableSpawns];

    static ControlRow T(string control, string name, string cls, string[] bad, string[] good, string[] empty) =>
        new(control, name, "test", cls, bad.Select(x => $"{name}_fails_when_{x}").ToArray(), good.Select(x => $"{name}_passes_{x}").ToArray(),
            empty.Select(x => $"{name}_empty_{x}").ToArray());

    static ControlRow C(string control, string name, string check, string[] bad, string[] good, string[] empty) =>
        new(control, name, "cmd", check, bad, good, empty);

    /// <summary>The test classes the listed plans add (event-library's seven, walkable-spawns' two); every test method
    /// in them follows one of the three forms.</summary>
    public static readonly string[] NewClasses =
    [
        "TemplateLibraryTests", "TemplateCommandTests", "AuthoringTests", "AuthoringCapacityTests", "PillarSwitchTests",
        "ReadinessTests", "LibraryDependencyFailureTests", "SpawningTests", "HealthTests",
    ];

    /// <summary>The existing classes that gain the plans' cases; their earlier methods keep their names.</summary>
    public static readonly string[] ExistingClasses =
        ["CommandArgTests", "ConfigChangedTests", "AuthorizationTests", "ContractDocTests", "ControlPrecedenceTests", "DependencyFailureTests"];

    public static readonly IReadOnlyList<ControlRow> Table =
    [
        T("D1", "TemplateCatalogue", "TemplateLibraryTests", ["template_breaks_a_rule", "file_is_broken"], ["real_file"], ["zero_templates"]),
        T("D2", "StarterTemplates", "TemplateLibraryTests", ["a_field_differs"], ["six_as_business_rules"], ["no_templates"]),
        T("D4", "TemplateList", "TemplateCommandTests", ["arguments_are_bad", "catalogue_unavailable", "info_id_unknown"],
            ["real_catalogue", "invalid_template_with_reason", "three_pages", "long_trigger_within_one_message"], ["catalogue"]),
        T("D5", "TemplateUse", "TemplateCommandTests", ["refused", "used_twice"], ["copy_disabled", "enabled_template_copied_disabled"], ["catalogue_unavailable"]),
        T("D6", "New", "AuthoringTests", ["refused"], ["skeleton"], ["events_file"]),
        T("D7", "Copy", "AuthoringTests", ["refused"], ["verbatim_disabled", "invalid_source_keeps_reason"], ["events_file"]),
        T("D8", "DeleteArming", "AuthoringTests", ["confirm_not_armed", "running", "deleted_by_another_admin"], ["confirm_within_30s"], ["no_pending_delete"]),
        T("D9", "TriggerFields", "AuthoringTests", ["value_is_bad", "duplicate_day", "duplicate_time", "duplicate_boss", "other_trigger_type"],
            ["each_type"], ["value"]),
        T("D9", "TriggerFields", "CommandArgTests", ["characters_are_bad"], ["well_formed"], ["list_entry"]),
        T("D10", "ActionFields", "AuthoringTests", ["value_is_bad", "duplicate_unit", "duplicate_faction"],
            ["factions_and_units", "unknown_name_disabled_by_reload"], ["value"]),
        T("D10", "ActionFields", "CommandArgTests", ["name_breaks_rule"], ["96_character_name"], ["list_entry"]),
        T("D11", "Location", "AuthoringTests", ["refused"], ["point_rounded"], ["no_character"]),
        T("D12", "Equivalence", "AuthoringTests", ["file_changed_on_disk", "stale_file_alters_the_plan", "newer_schema", "over_1_mb"],
            ["write_equals_hand_edit", "two_admin_sequential_set", "running_instance_keeps_end_time"], ["refused_plan"]),
        T("D12", "Equivalence", "ConfigChangedTests", ["write_refused"], ["one_notice_per_applied_write"], ["events_file"]),
        T("D13", "PillarMap", "PillarSwitchTests", ["map_and_keys_differ"], ["settings_keys"], ["settings_text"]),
        T("D14", "PillarCommand", "PillarSwitchTests", ["off_processed_before_start", "no_change", "hand_edit_already_set_it", "reload_throws"],
            ["list", "switch_on_and_off", "reload_keeps_hand_edit", "ordering_start_before_off", "off_after_hand_edit_ends_running"], ["nothing_running"]),
        T("D16", "Readiness", "ReadinessTests", ["blocker_has_no_label", "a_control_blocks"],
            ["every_combination", "combination_count", "labels_one_to_one", "list_master_line_and_running"], ["no_events"]),
        T("D16", "Readiness", "ControlPrecedenceTests", ["blocker_unlabelled"], ["same_cause_as_start_refusal"], ["open_controls"]),
        T("D17", "LibraryKinds", "AuthorizationTests", ["not_admin"], ["admin_only"], ["work_reply"]),
        C("D17", "VcfDependency", "preflight -AuthSuite › Test-CheckVcfDependency",
            ["selftest VcfDependency/bad", "selftest VcfDependency/bad-2", "selftest VcfDependency/bad-3"], ["selftest VcfDependency/good"], ["selftest VcfDependency/empty"]),
        C("D18", "CfgWrites", "preflight -SelfTest › Test-CheckCfgWrites",
            ["selftest CfgWrites/bad", "selftest CfgWrites/bad-2", "selftest TemplatesJson/bad-3"], ["selftest CfgWrites/good"], ["selftest CfgWrites/empty"]),
        T("D19", "SaveFailure", "PillarSwitchTests", ["save_truncates_then_throws", "file_lacks_or_garbles_key"], ["line_written_then_throws", "off_ends_events_when_file_says_off", "file_values_read"], ["file_after_truncate"]),
        T("D19", "EventsWrite", "LibraryDependencyFailureTests", ["tmp_write_fails"], ["after_disk_recovers"], ["refused_plan_writes_nothing"]),
        T("D19", "EventsPromote", "LibraryDependencyFailureTests", ["promote_replaces_then_throws"], ["throw_before_replace_keeps_file"], ["events_file"]),
        T("D19", "StateWrite", "LibraryDependencyFailureTests", ["state_write_fails", "state_read_only"], ["both_writes"], ["no_cooldown_row", "null_cooldown_rows"]),
        T("D19", "Catalogue", "LibraryDependencyFailureTests", ["missing_or_unparsable"], ["real_file"], ["zero_templates"]),
        T("D19", "LocationContext", "LibraryDependencyFailureTests", ["no_character"], ["character_in_world"], ["console_context"]),
        T("D19", "PhaseSource", "LibraryDependencyFailureTests", ["read_throws"], ["edges"], ["no_phase_hook"]),
        T("D20", "SettableFields", "CommandArgTests", ["name_outside_table"], ["every_table_name"], ["field_name"]),
        T("D20", "CommandForms", "CommandArgTests", ["form_is_malformed"], ["each_declared_form"], ["arguments"]),
        T("D20", "DesignDoc", "ContractDocTests", ["table_differs"], ["forms_and_fields_match"], ["section"]),
        C("D24", "SoakReport", "soak-report.ps1 -SelfTest",
            ["selftest bad-unpaired", "selftest bad-tick", "selftest bad-unhandled", "selftest bad-short", "selftest bad-missing-template"],
            ["selftest good"], ["selftest empty"]),
        T("D27", "Capacity", "AuthoringCapacityTests", ["file_holds_200"],
            ["creating_form_writes_200th", "set_and_delete_at_200", "each_write_under_200ms_at_199", "near_1mb_set_and_delete_under_200ms"], ["events_file"]),
        C("D28", "ReleaseVerify", "release-verify.ps1 -SelfTest",
            ["selftest missing asset", "selftest differing hash"], ["selftest hashes equal"], ["selftest no release"]),
        C("D29", "RollbackRoutes", "preflight -RollbackOf › RollbackRoutes",
            ["selftest RollbackRoutes/bad", "selftest RollbackRoutes/bad-2", "selftest RollbackRoutes/bad-3"], ["selftest RollbackRoutes/good"], ["selftest RollbackRoutes/empty"]),
        C("D30", "DataInventory", "preflight -SelfTest › data inventory and -Paths -DeclaredOf",
            ["selftest DataInventory/bad-el", "selftest DataInventory/bad-tmp", "selftest DataInventory/bad-tmp-2", "selftest Paths/bad-undeclared", "selftest Paths/bad-soak"],
            ["selftest DataInventory/good"], ["selftest DataInventory/empty"]),
        T("D31", "ControlCases", "ControlCaseTests", ["table_breaks_a_rule"], ["plan_and_table_agree"], ["plan_without_controls"]),
        T("D32", "Degraded", "LibraryDependencyFailureTests", ["write_uncertain"], ["cleared_by_clean_write_or_reload"], ["healthy"]),
        C("D33", "DependencySuite", "preflight -DependencySuite",
            ["selftest DependencySuite/bad", "selftest DependencySuite/bad-2"], ["selftest DependencySuite/good"], ["selftest DependencySuite/empty"]),
        C("D34", "TestRuns", "preflight Invoke-ClassTests",
            ["selftest TestRuns/bad", "selftest TestRuns/bad-2", "selftest TestRuns/bad-3"], ["selftest TestRuns/good"], ["selftest TestRuns/empty"]),
        T("D36", "SlowTick", "EngineTests", ["tick_reaches_250ms", "ticks_repeat_within_a_minute"], ["ticks_under_250ms"], ["no_phase_timings"]),

        // ---- walkable-spawns (D9): a cmd row names the fixtures under tools/ that fail and pass it
        T("D2", "SpawnPoints", "SpawningTests", ["all_blocked", "blocked_point_kept"],
            ["free_ring_point_kept", "search_order", "first_free_in_order", "centre_when_only_centre_free"], ["radius_zero"]) with { Plan = WalkableSpawns },
        T("D3", "WavePoints", "SpawningTests", ["not_grounded", "budget_spent", "budget_spent_mid_search", "check_throws", "point_is_nan"],
            ["one_point_per_unit_in_order", "blocked_ring_point_moved", "counts_match_kinds"], ["no_probe"]) with { Plan = WalkableSpawns },
        T("D5", "WalkCheck", "DependencyFailureTests",
            ["check_throws", "streak_repeats", "unanswered_wave_closes_streak", "tile_world_missing", "height_out_of_range"],
            ["recovered_check_used_again", "height_in_range"], ["no_height"]) with { Plan = WalkableSpawns },
        T("D6", "WalkHealth", "HealthTests", ["streak_open"], ["recovered_check_clears_entry"], ["healthy"]) with { Plan = WalkableSpawns },
        T("D7", "WalkRadius", "CommandArgTests", ["refused"], ["in_range"], ["absent"]) with { Plan = WalkableSpawns },
        C("D9", "DebugCommands", "preflight › Test-CheckDebugCommands",
            ["tools/preflight-fixtures/DebugCommands/bad", "tools/preflight-fixtures/DebugCommands/bad-2", "tools/preflight-fixtures/DebugCommands/bad-3",
             "tools/preflight-fixtures/DebugCommands/bad-4", "tools/preflight-fixtures/DebugCommands/bad-5", "tools/preflight-fixtures/DebugCommands/bad-6",
             "tools/preflight-fixtures/DebugCommands/bad-7", "tools/preflight-fixtures/DebugCommands/bad-8"],
            ["tools/preflight-fixtures/DebugCommands/good"], ["tools/preflight-fixtures/DebugCommands/empty"]) with { Plan = WalkableSpawns },
        C("D10", "ProbeRecords", "preflight -SessionsOf › Test-CheckSessionLogs",
            ["tools/preflight-fixtures/SessionLogs/bad-probe", "tools/preflight-fixtures/SessionLogs/bad-probe-2", "tools/preflight-fixtures/SessionLogs/bad-probe-3",
             "tools/preflight-fixtures/SessionLogs/bad-probe-4", "tools/preflight-fixtures/SessionLogs/bad-probe-5", "tools/preflight-fixtures/SessionLogs/bad-probe-6"],
            ["tools/preflight-fixtures/SessionLogs/good"], ["tools/preflight-fixtures/SessionLogs/empty"]) with { Plan = WalkableSpawns },
        C("D11", "Secrets", "preflight › Test-CheckSecrets",
            ["tools/preflight-fixtures/Secrets/bad", "tools/preflight-fixtures/Secrets/bad-9"],
            ["tools/preflight-fixtures/Secrets/good"], ["tools/preflight-fixtures/Secrets/empty"]) with { Plan = WalkableSpawns },
        C("D12", "KnownIssue", "preflight › Test-CheckChangelogs",
            ["tools/preflight-fixtures/Changelogs/bad-knownissue", "tools/preflight-fixtures/Changelogs/bad-knownissue-2"], ["tools/preflight-fixtures/Changelogs/good"],
            ["tools/preflight-fixtures/Changelogs/empty"]) with { Plan = WalkableSpawns },
    ];
}
