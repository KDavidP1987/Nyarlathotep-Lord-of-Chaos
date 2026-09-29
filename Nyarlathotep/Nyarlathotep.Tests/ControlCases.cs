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
    public const string RaphaelApiAdmin = "raphael-api-admin";
    public const string Regions = "regions";

    /// <summary>The plans whose controls the table lists; each is copied to the test output under Resources/.</summary>
    public static readonly string[] Plans = [EventLibrary, WalkableSpawns, RaphaelApiAdmin, Regions];

    static ControlRow T(string control, string name, string cls, string[] bad, string[] good, string[] empty) =>
        new(control, name, "test", cls, bad.Select(x => $"{name}_fails_when_{x}").ToArray(), good.Select(x => $"{name}_passes_{x}").ToArray(),
            empty.Select(x => $"{name}_empty_{x}").ToArray());

    static ControlRow C(string control, string name, string check, string[] bad, string[] good, string[] empty) =>
        new(control, name, "cmd", check, bad, good, empty);

    /// <summary>The test classes the listed plans add (event-library's seven, walkable-spawns' two, raphael-api-admin's
    /// five, regions' EventAdminTests); every test method in them follows one of the three forms.</summary>
    public static readonly string[] NewClasses =
    [
        "TemplateLibraryTests", "TemplateCommandTests", "AuthoringTests", "AuthoringCapacityTests", "PillarSwitchTests",
        "ReadinessTests", "LibraryDependencyFailureTests", "SpawningTests", "HealthTests",
        "HumanReplyTests", "OutcomeCodeTests", "ApiTwinTests", "RateGateTests", "ApiOverloadTests", "EventAdminTests",
    ];

    /// <summary>The existing classes that gain the plans' cases; their earlier methods keep their names.</summary>
    public static readonly string[] ExistingClasses =
        ["CommandArgTests", "ConfigChangedTests", "AuthorizationTests", "ContractDocTests", "ControlPrecedenceTests", "DependencyFailureTests",
         "ApiLinesTests", "WireFormatTests", "PushTests", "PrivacyTests"];

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
        // ---- raphael-api-admin (D11): a cmd row names the fixtures under tools/ that fail and pass it
        T("D1", "HumanReply", "HumanReplyTests", ["reply_differs"], ["captured_rows", "every_row_has_a_scenario", "newest_capture_wins"], ["capture"])
            with { Plan = RaphaelApiAdmin },
        C("D1", "HumanReplies", "preflight › Test-CheckHumanReplies and Test-CheckOutcomeReturns",
            ["tools/preflight-fixtures/HumanReplies/bad", "tools/preflight-fixtures/HumanReplies/bad-2", "tools/preflight-fixtures/HumanReplies/bad-3", "tools/preflight-fixtures/HumanReplies/bad-4", "tools/preflight-fixtures/HumanReplies/bad-5", "tools/preflight-fixtures/OutcomeReturns/bad", "tools/preflight-fixtures/OutcomeReturns/bad-2", "tools/preflight-fixtures/OutcomeReturns/bad-3"],
            ["tools/preflight-fixtures/HumanReplies/good", "tools/preflight-fixtures/OutcomeReturns/good"], ["tools/preflight-fixtures/HumanReplies/empty", "tools/preflight-fixtures/OutcomeReturns/empty"])
            with { Plan = RaphaelApiAdmin },
        T("D2", "OutcomeCode", "OutcomeCodeTests", ["cell_changes", "a_code_has_no_twin", "built_outside_factories"],
            ["each_refusal_row", "every_row_has_a_case", "codes_map_one_to_one", "reasons_closed_list"], ["plan_table"]) with { Plan = RaphaelApiAdmin },
        T("D3", "Event", "ApiTwinTests", ["verb_unknown", "argument_bad", "human_badarg_would_log", "refused"],
            ["each_verb_ok_line", "gateway_kind_before_op", "log_line_prefixed", "one_grammar_line_each"], ["arguments"]) with { Plan = RaphaelApiAdmin },
        T("D4", "TemplatePillarPurge", "ApiTwinTests", ["argument_bad", "pillar_save_fails", "purge_confirm_lacks_cooldown"], ["ok_lines"], ["nothing_to_purge"])
            with { Plan = RaphaelApiAdmin },
        T("D5", "Idempotency", "ApiTwinTests", ["start_or_stop_repeats", "confirm_is_not_the_askers"],
            ["held_enable_writes_nothing", "held_pillar_writes_nothing", "held_off_ends_running", "set_to_held_value_writes", "human_ask_twin_confirm",
             "two_admins_confirm_in_one_second"], ["confirm_without_ask"]) with { Plan = RaphaelApiAdmin },
        T("D6", "RateGate", "RateGateTests", ["sixth_within_a_second", "gate_full_of_active_admins"],
            ["five_in_a_second", "sliding_window", "admins_apart", "idle_admins_pruned", "clock_stepped_back"], ["no_prior_twins"]) with { Plan = RaphaelApiAdmin },
        T("D6", "RateOrder", "ApiTwinTests", ["sixth_twin_runs"], ["reads_and_human_not_counted", "one_second_later_and_per_admin"], ["first_twin"])
            with { Plan = RaphaelApiAdmin },
        T("D7", "AdminReads", "ApiLinesTests", ["cooldown_is_over", "catalogue_unavailable", "filter_or_page_is_bad"],
            ["templates_rows", "template_info", "pillar_list_in_order", "killswitch_during_cooldown", "through_the_flows"], ["no_templates_no_events"])
            with { Plan = RaphaelApiAdmin },
        T("D8", "AdminTwins", "WireFormatTests", ["key_order_differs", "forbidden_character_given"], ["contract_examples", "longest_values_cut"], ["value"])
            with { Plan = RaphaelApiAdmin },
        T("D9", "Twins", "ConfigChangedTests", ["refused_or_unchanged"], ["same_pushes_as_human", "changed_pillar_one_notice"], ["reads_queue_nothing"])
            with { Plan = RaphaelApiAdmin },
        T("D9", "Twins", "PushTests", ["refused"], ["start_stop_purge_as_human", "pillar_off_ends_running"], ["nothing_to_purge"]) with { Plan = RaphaelApiAdmin },
        T("D10", "ContractApi4", "ContractDocTests", ["contract_regresses", "handoff_or_design_lags"], ["contract_handoff_and_design"], ["contract"])
            with { Plan = RaphaelApiAdmin },
        C("D10", "WireContract", "preflight › Test-CheckWireContract", ["tools/preflight-fixtures/WireContract/bad", "tools/preflight-fixtures/WireContract/bad-2", "tools/preflight-fixtures/WireContract/bad-3", "tools/preflight-fixtures/WireContract/bad-9"],
            ["tools/preflight-fixtures/WireContract/good"], ["tools/preflight-fixtures/WireContract/empty"]) with { Plan = RaphaelApiAdmin },
        T("D11", "ApiOverload", "ApiOverloadTests", ["two_commands_share_a_count"], ["real_table"], ["table"]) with { Plan = RaphaelApiAdmin },
        T("D11", "ControlCases", "ControlCaseTests", ["table_breaks_a_rule"], ["plan_and_table_agree"], ["plan_without_controls"]) with { Plan = RaphaelApiAdmin },
        C("D11", "AdminStatic", "preflight -AuthSuite › Test-CheckCommands, Test-CheckGatewayOnly and Test-CheckWireContract",
            ["tools/preflight-fixtures/Commands/bad-2", "tools/preflight-fixtures/Commands/bad-3", "tools/preflight-fixtures/Commands/bad-4", "tools/preflight-fixtures/GatewayOnly/bad-6", "tools/preflight-fixtures/GatewayOnly/bad-7", "tools/preflight-fixtures/GatewayOnly/bad-8", "tools/preflight-fixtures/WireContract/bad-10", "tools/preflight-fixtures/WireContract/bad-11",
             "tools/preflight-fixtures/WireContract/bad-12", "tools/preflight-fixtures/WireContract/bad-13"],
            ["tools/preflight-fixtures/Commands/good", "tools/preflight-fixtures/GatewayOnly/good", "tools/preflight-fixtures/WireContract/good"],
            ["tools/preflight-fixtures/Commands/empty", "tools/preflight-fixtures/GatewayOnly/empty", "tools/preflight-fixtures/WireContract/empty"]) with { Plan = RaphaelApiAdmin },
        T("D12", "Twin", "DependencyFailureTests", ["op_throws", "other_twin_op_throws", "streak_repeats", "save_fails"],
            ["throw_after_clean_run_logged_again", "memory_equals_file_after_refused_write", "throw_after_change_is_internal"], ["clean_run_warns_nothing"]) with { Plan = RaphaelApiAdmin },
        C("D14", "Records", "preflight -AuditOf, -SessionsOf and -Paths -DeclaredOf › AuditSteps, SessionLogs, Paths and DataInventory",
            ["tools/preflight-fixtures/AuditSteps/bad", "tools/preflight-fixtures/AuditSteps/bad-2", "tools/preflight-fixtures/SessionLogs/bad", "tools/preflight-fixtures/SessionLogs/bad-2", "tools/preflight-fixtures/Paths/bad-undeclared", "tools/preflight-fixtures/Paths/bad-temp", "tools/preflight-fixtures/DataInventory/bad", "tools/preflight-fixtures/DataInventory/bad-2"],
            ["tools/preflight-fixtures/AuditSteps/good", "tools/preflight-fixtures/SessionLogs/good", "tools/preflight-fixtures/Paths/good", "tools/preflight-fixtures/DataInventory/good"],
            ["tools/preflight-fixtures/AuditSteps/empty", "tools/preflight-fixtures/SessionLogs/empty", "tools/preflight-fixtures/Paths/empty", "tools/preflight-fixtures/DataInventory/empty"]) with { Plan = RaphaelApiAdmin },
        C("D15", "Secrets", "preflight › Test-CheckSecrets", ["tools/preflight-fixtures/Secrets/bad", "tools/preflight-fixtures/Secrets/bad-9"], ["tools/preflight-fixtures/Secrets/good"],
            ["tools/preflight-fixtures/Secrets/empty"]) with { Plan = RaphaelApiAdmin },
        T("D15", "Twins", "PrivacyTests", ["a_line_carries_the_id"], ["no_id_name_or_position", "location_here_value_only"], ["no_events"]) with { Plan = RaphaelApiAdmin },
        C("D16", "Release", "preflight › Test-CheckVersion; -RollbackOf › RollbackRoutes; release-verify.ps1 -SelfTest",
            ["tools/preflight-fixtures/Version/bad", "tools/preflight-fixtures/RollbackRoutes/bad", "tools/preflight-fixtures/RollbackRoutes/bad-2", "tools/preflight-fixtures/RollbackRoutes/bad-3", "selftest missing asset", "selftest differing hash"],
            ["tools/preflight-fixtures/Version/good", "tools/preflight-fixtures/RollbackRoutes/good", "selftest hashes equal"],
            ["tools/preflight-fixtures/Version/empty", "tools/preflight-fixtures/RollbackRoutes/empty", "selftest no release"]) with { Plan = RaphaelApiAdmin },
        // ---- regions (D11): the test rows name the form-named cases; a cmd row names the fixtures under tools/
        T("D1", "Point", "RegionTests", ["not_finite", "polygon_degenerate_or_untagged"], ["each_shape", "first_polygon_in_index_order"], ["index"])
            with { Plan = Regions },
        C("D2", "RegionsLine", "preflight -LogCheck › Test-CheckLogCheck",
            ["tools/preflight-fixtures/LogCheck/bad-regions-missing", "tools/preflight-fixtures/LogCheck/bad-regions-zero",
             "tools/preflight-fixtures/LogCheck/bad-regions-differ", "tools/preflight-fixtures/LogCheck/bad-regions-noversion"],
            ["tools/preflight-fixtures/LogCheck/good-regions", "tools/preflight-fixtures/LogCheck/good-regions-restart"], ["tools/preflight-fixtures/LogCheck/empty"]) with { Plan = Regions },
        T("D3", "Scope", "EventValidationTests", ["invalid", "name_not_on_map"],
            ["every_trigger_type", "trigger_and_action_independent", "case_insensitive_names_in_games_spelling"], ["absent_is_global"]) with { Plan = Regions },
        T("D4", "Region", "TriggerActivationTests", ["kill_position_unreadable"], ["kill_inside_regions", "schedule_due_once"], ["global_trigger", "global_kill_reads_no_position"])
            with { Plan = Regions },
        T("D4", "ScopeGate", "EngineTests", ["no_reader_or_lookup", "kill_outside_scope", "admin_start_without_player_inside", "position_source_throws"],
            ["player_in_regions", "in_region_kill_without_player", "skip_line_only_for_system_starts"], ["global_trigger"]) with { Plan = Regions },
        T("D4", "RegionRefusal", "OutcomeCodeTests", ["outside_or_no_player"], ["player_inside"], ["no_players"]) with { Plan = Regions },
        T("D5", "Region", "EmpowerEligibilityTests", ["position_unreadable"],
            ["in_region_applied_out_of_region_skipped", "skip_order", "carried_unit_keeps_carrier"], ["global_scope"]) with { Plan = Regions },
        T("D6", "RegionWaves", "SpawningTests",
            ["out_of_scope_point_used", "scope_spends_budget", "budget_spent_keeps_outside_ring_point", "fall_open_places_outside_ring_point"],
            ["in_scope_placement"], ["global_no_scope_call"]) with { Plan = Regions },
        T("D7", "Regions", "DependencyFailureTests", ["no_polygons", "build_throws", "log_sink_throws", "names_differ"],
            ["unavailable_wins_over_not_on_map", "reload_rebuilds_and_clears_health", "built_before_definitions", "read_only_map"],
            ["healthy_build_leaves_no_health_entry"]) with { Plan = Regions },
        T("D8", "Lines", "RegionTests", ["definition_disabled", "here_position_unreadable"], ["list_counts_per_region_then_global", "here_region_outside_or_unavailable"], ["no_events"])
            with { Plan = Regions },
        T("D9", "Scope", "EventAdminTests", ["set_value_reload_rejects", "set_more_names_than_regions", "trigger_type_changes"],
            ["info_shows_both_scopes", "list_suffix_regional_only", "set_writes_games_spelling", "set_every_trigger_type", "edit_leaves_running_instance"],
            ["value"]) with { Plan = Regions },
        T("D10", "Region", "ApiLinesTests", ["longest_values_with_ten_regions"],
            ["rows_carry_action_scope", "pushes_on_start_and_end_only", "read_counts_in_game_order"], ["no_active_events"]) with { Plan = Regions },
        C("D10", "WireContract", "preflight › Test-CheckWireContract", ["tools/preflight-fixtures/WireContract/bad", "tools/preflight-fixtures/WireContract/bad-2", "tools/preflight-fixtures/WireContract/bad-3", "tools/preflight-fixtures/WireContract/bad-9"],
            ["tools/preflight-fixtures/WireContract/good"], ["tools/preflight-fixtures/WireContract/empty"]) with { Plan = Regions },
        T("D11", "ControlCases", "ControlCaseTests", ["table_breaks_a_rule"], ["plan_and_table_agree"], ["plan_without_controls"]) with { Plan = Regions },
        C("D11", "RegionCommand", "preflight -AuthSuite › Test-CheckCommands", ["tools/preflight-fixtures/Commands/bad-13"],
            ["tools/preflight-fixtures/Commands/good"], ["tools/preflight-fixtures/Commands/empty"]) with { Plan = Regions },
        C("D13", "Records", "preflight -AuditOf, -SessionsOf and -Paths -DeclaredOf › AuditSteps, SessionLogs, Paths and DataInventory",
            ["tools/preflight-fixtures/AuditSteps/bad", "tools/preflight-fixtures/AuditSteps/bad-2", "tools/preflight-fixtures/SessionLogs/bad", "tools/preflight-fixtures/SessionLogs/bad-2", "tools/preflight-fixtures/Paths/bad-undeclared", "tools/preflight-fixtures/Paths/bad-temp", "tools/preflight-fixtures/DataInventory/bad", "tools/preflight-fixtures/DataInventory/bad-2"],
            ["tools/preflight-fixtures/AuditSteps/good", "tools/preflight-fixtures/SessionLogs/good", "tools/preflight-fixtures/Paths/good", "tools/preflight-fixtures/DataInventory/good"],
            ["tools/preflight-fixtures/AuditSteps/empty", "tools/preflight-fixtures/SessionLogs/empty", "tools/preflight-fixtures/Paths/empty", "tools/preflight-fixtures/DataInventory/empty"]) with { Plan = Regions },
        C("D14", "Secrets", "preflight › Test-CheckSecrets", ["tools/preflight-fixtures/Secrets/bad", "tools/preflight-fixtures/Secrets/bad-9"], ["tools/preflight-fixtures/Secrets/good"],
            ["tools/preflight-fixtures/Secrets/empty"]) with { Plan = Regions },
        T("D14", "Region", "PrivacyTests", ["a_line_carries_the_position"], ["no_position_name_or_id"], ["global_event"]) with { Plan = Regions },
        T("D15", "Cost", "RegionTests", ["fixture_missing_or_zero"], ["10000_points_over_a_real_sized_index", "a_sweep_reads_one_position_per_unit"],
            ["global_scope_reads_no_position"]) with { Plan = Regions },
        C("D16", "Release", "preflight › Test-CheckVersion; -RollbackOf › RollbackRoutes; release-verify.ps1 -SelfTest",
            ["tools/preflight-fixtures/Version/bad", "tools/preflight-fixtures/RollbackRoutes/bad", "tools/preflight-fixtures/RollbackRoutes/bad-2", "tools/preflight-fixtures/RollbackRoutes/bad-3", "selftest missing asset", "selftest differing hash"],
            ["tools/preflight-fixtures/Version/good", "tools/preflight-fixtures/RollbackRoutes/good", "selftest hashes equal"],
            ["tools/preflight-fixtures/Version/empty", "tools/preflight-fixtures/RollbackRoutes/empty", "selftest no release"]) with { Plan = Regions },
    ];
}
