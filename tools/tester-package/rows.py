"""rows.py: the single source of truth for the Nyarlathotep tester package (docs: _shared/TESTING-STANDARDS.md).

build.py renders every artefact (xlsx, pdf, csv, html) from this module and refuses any key it does not render.
Rows are written for the tester: what they see in game and the commands they type, never file paths or code names.
"""

PRODUCT = "Nyarlathotep"
ROUND = {
    "slug": "dev-round-01-production-setups",
    "title": "Nyarlathotep, Lord of Chaos — dev server test round 1: events set up as a real server would run them",
    "build": "test build 0.13.0-rc.3 (apocalypse, unit watch, faction wave templates, pursuit and the timed and onslaught waves, before release)",
    # the GitHub pre-release holding this round's mod zip, workbook and reading copy
    "download": "https://github.com/KDavidP1987/Nyarlathotep-Lord-of-Chaos/releases/tag/v0.13.0-rc.3",
    "date": "2026-10-16",
}

# Two testers by default; rename them on the Start here sheet before you begin.
TESTERS = ["Tester A", "Tester B"]

COVER_FIELDS = [
    ("Name", "So we know whose notes these are when two of you return one file."),
    ("Timezone", "Schedules run on the server's clock; your own time zone explains a 'wrong hour' report."),
    ("PC (make, model, rough age)", "Frame drops during large fights depend on the machine as much as the server."),
    ("Windows version", "Rarely matters for a server mod, but costs nothing and closes a question."),
    ("V Rising version (title screen)", "A client older than the server cannot join; it also ties a report to a game patch."),
    ("Display size and scaling", "Chat lines can wrap or clip at high scaling; a cut-off line is otherwise unattributable."),
    ("Input (mouse and keyboard, controller)", "Some rows ask you to climb, kite or stand still; input changes how that goes."),
    ("Connection (wired, wifi, rough ping)", "Units that jump or rubber-band can be lag on your side rather than the mod."),
    ("Character gear level at start", "Event difficulty is set by level; your level explains 'too easy' or 'too hard'."),
    ("Admin on the dev server? (yes or no)", "Setting up events needs admin rights; rows marked admin come back Blocked without them."),
    ("Date started", "Ties the round to a build; the dev server may be updated during the round."),
    ("Date finished", "As above."),
    ("Rough hours spent", "Tells us whether the round is the right size for next time."),
]

LAST_ROUND = ("This is the first round, so there are no earlier findings to account for. From round two, this block "
              "lists every finding you sent, by your reference, and what happened to it.")

COULD_NOT_CHECK = [
    "Anything that needs a second player. Our own test tool plays one character alone, and it is invulnerable, so "
    "monsters never truly chase it. Every row that needs two of you says so.",
    "A real player in danger. Because our character cannot be hurt, we could never see a monster that wants to "
    "reach a player but cannot (the stuck-unit rows), or how an ambush feels when it can actually kill you.",
    "How it feels. Whether a fight is fun, an announcement reads well, a jump looks natural, or the server "
    "stays smooth while a hundred monsters roam: only people playing can judge that.",
    "Real triggers. We start events by command. A schedule, nightfall, a V Blood kill, walking into a region and "
    "killing a faction's people are the ways events start on a real server, and they need real play.",
]

# Installing the build on the test server: done once by whoever runs that server (the owner, or a tester running
# their own). Each step is "action -> what you should see", like a row's steps.
INSTALL = [
    "Open the download page (link above) and download Nyarlathotep-0.13.0-rc.3.zip, this workbook and its reading "
    "copy -> the zip holds Nyarlathotep.dll and a short install note.",
    "Stop the V Rising dedicated server -> a running server locks its mod files, so a copy over them fails.",
    "Install BepInExPack V Rising 1.733.2 on the server, with a mod manager's dedicated-server profile or by unzipping "
    "it into the server's folder so the BepInEx folder sits next to VRisingServer.exe; start the server once and stop "
    "it -> BepInEx creates its plugins and config folders.",
    "Put VampireCommandFramework 0.10.4 (VampireCommandFramework.dll) in the server's BepInEx/plugins folder -> it is "
    "the chat command library the mod needs.",
    "Put Nyarlathotep.dll from the zip in the same BepInEx/plugins folder, replacing any older copy -> one copy only.",
    "Start the server and open BepInEx/LogOutput.log -> a line 'Plugin kdpen.Nyarlathotep v0.13.0-rc.3 loaded' "
    "appears, and BepInEx/config gains kdpen.Nyarlathotep.cfg.",
    "So players see events as they would on a live server: stop the server, open BepInEx/config/kdpen.Nyarlathotep.cfg, "
    "set EventBanners = true under [Announcements], save, start the server -> from now on everyone gets a server-wide "
    "line when an event starts and when it ends.",
    "Make each tester an admin: add their Steam ID (the 17-digit number) on its own line to adminlist.txt in the "
    "server's save data Settings folder, then restart the server -> in game, with the console switched on in "
    "Options, press the ~ key and type adminauth.",
    "In game, type .nyar in chat -> the first line reads 'Nyarlathotep, Lord of Chaos (v0.13.0-rc.3)' and the admin "
    "commands are listed. Everything ships switched off; each row turns on what it needs.",
    "For a newer test build later: stop the server, replace Nyarlathotep.dll, start it -> the version line shows the "
    "new build. To remove the mod: type .nyar purge then .nyar purge confirm, stop the server, delete the file -> "
    "no monster of ours is left behind.",
]

ACCESS = ("Connect to the dev server, the test server set up with the steps under 'Installing the build', at the "
          "address its admin gives you (Direct Connect, not the server list). "
          "{admin_rows} of the {all_rows} rows need admin rights: the owner adds you as an admin before the round, and you "
          "type '.nyar' in chat to confirm you see the admin commands. Rows that need admin say so under 'You will need'.")

RULES = [
    "Put a name against every row in 'Assigned to' before either of you begins.",
    "Fill in 'Tested at' (date and time) on every row you test.",
    "If a row is not a clean Pass, fill in 'If not a clean pass — what is still wrong'. It is not optional.",
    "Say so plainly if something is wrong. A finding we disagree with is still worth having; give the evidence "
    "(a screenshot, the exact chat line) if you have it.",
    "If the two of you disagree about whether something is a fault, put both views in. A disagreement is a finding.",
    "Anything not on a row goes on 'Anything new'. Small things are welcome.",
    "Blocked is a real answer. If a row needs a second player or admin rights you do not have, mark it Blocked and "
    "move on rather than improvising.",
    "Chat commands are typed exactly as written, including the dot at the start. Text in angle brackets, such as "
    "<id>, is replaced by the value named.",
]

RESULTS = [
    ("Pass", "Does what the row says, and nothing bothered you."),
    ("Partial pass", "Anything in between: most steps work and one does not, or it works but something else went wrong."),
    ("Fail", "Does not do what the row says."),
    ("Blocked", "Could not reach it: no second player, no admin rights, the server was down."),
    ("Not tested yet", "Nobody has started it."),
]
STEP_RESULTS = [("OK", "The step did what it says."), ("Problem", "It did not, or something odd happened."),
                ("Skipped", "You could not do this step.")]

CATEGORIES = [
    ("retest", "You reported it — please confirm the fix"),
    ("changed", "Changed since your last round"),
    ("new", "New"),
    ("explore", "Established feature — a fresh pair of eyes"),
    ("check", "Not an ordinary test row"),
]

KNOWN_LIMITS = [
    ("Monsters spawned by an event do not scale to how many players are near or how strong they are.",
     "Scaling is the next piece of work after Halloween. For now the admin sets levels and counts in each event."),
    ("A monster that dies before it ever reaches a player is not replaced.",
     "Replacing them is planned after Halloween. Until then, killing monsters from far away or from a castle edge "
     "shortens an event; we leave that to players' honour."),
    ("Revenge heat (how angry a faction is with you) is forgotten when the server restarts.",
     "Deliberate: nothing about players is saved to disk."),
    ("Monsters far from every player can move in short hidden jumps rather than walking.",
     "Deliberate: walking across the map is unreliable in this game. A jump only happens while no player is within "
     "100 m, so you should never see one. If you do, that is a finding."),
    ("A stuck monster is moved out of sight up to twice, then removed with no kill credit.",
     "Deliberate (the unit watch). A removed monster counts as cleared for its wave, so a wave cannot hang on one "
     "you cannot reach."),
    ("No event spawns monsters inside a claimed castle's territory, and none jump into one.",
     "Deliberate. A wave centred on a castle is skipped; players standing in their castle are never picked as targets."),
    ("After '.nyar purge confirm', events refuse to start for about 4 minutes.",
     "Deliberate cool-down after the emergency stop."),
    ("An ambush at a tight spot (cliffs, walls, water) can appear closer than 20-40 m.",
     "Known: when no walkable ground is found at that distance, the spawn moves closer. A wider search is planned."),
    ("Chat replies to admin commands are short and technical.",
     "They are meant for admins and a future in-game admin panel, not for players."),
    ("The zombie apocalypse runs for hours; this round shortens it to 2 hours.",
     "Its full length (4 hours by default) is an admin choice; 2 hours still brings all four hordes."),
]

# Row keys build.py renders. Any other key is a build error (TESTING-STANDARDS §6).
# Each step is "action -> what you should see".
ROWS = [
    {
        "id": "N-01", "status": "new", "area": "Getting set up",
        "title": "First look: the command list and the status line",
        "summary": "Every feature is an 'event' an admin sets up. Before anything else, check the basics answer you.",
        "needs": "Admin rights on the dev server.",
        "howToTest": [
            "Type .nyar in chat -> a list of the commands you may run appears, including admin ones.",
            "Type .nyar status -> it lists running events (probably none) and 'tracked units: 0'.",
            "Type .nyar template list -> the ready-made events are listed (empowerment and spawn events).",
            "Type .nyar pillar list -> it shows which kinds of event are switched on (empowerment, spawns).",
        ],
        "mostWanted": "Would an admin new to the mod know where to start from these replies?",
    },
    {
        "id": "N-02", "status": "new", "area": "Faction empowerment",
        "title": "Undead grow stronger at nightfall (a night-time event)",
        "summary": "The 'Undead nightfall' template buffs every undead NPC for 20 minutes when night falls in game.",
        "needs": "Admin rights; about 30 minutes that include an in-game nightfall; an undead area such as a Farbane "
                 "graveyard.",
        "howToTest": [
            "Type .nyar template use undead-nightfall -> it is copied into the server's events, switched off.",
            "Type .nyar pillar empowerment on, then .nyar event enable undead-nightfall -> both confirm (if it is "
            "already night in game, the event starts at once).",
            "Wait near undead NPCs until night falls in game -> a start line shows in chat and .nyar status lists "
            "the event with minutes left.",
            "Fight a few undead during the event, then again after it ends -> they hit harder and take more to kill "
            "during it, and are back to normal after.",
            "Type .nyar debug here 30 near undead during the event -> native NPCs are listed with their buff.",
        ],
        "mostWanted": "Could you feel the difference in the undead during the event without being told?",
        "deliberate": "Empowerment only buffs; it spawns nothing.",
    },
    {
        "id": "N-03", "status": "new", "area": "Faction empowerment",
        "title": "A weekly surge on a schedule (day and time)",
        "summary": "'Legion weekend surge' empowers the Legion on Saturdays at 20:00. Here you move its time to a few "
                   "minutes from now, as an admin planning a real evening would.",
        "needs": "Admin rights; the server's local time (ask the owner); a Legion area (Gloomrot or the Dunley "
                 "outskirts).",
        "howToTest": [
            "Type .nyar template use legion-weekend-surge and .nyar pillar empowerment on.",
            "Type .nyar event set legion-weekend-surge trigger.days <today, e.g. Wed> and .nyar event set "
            "legion-weekend-surge trigger.times <server time plus 5 minutes, e.g. 21:35> -> each confirms.",
            "Type .nyar event enable legion-weekend-surge, then .nyar event info legion-weekend-surge -> it shows the "
            "next start.",
            "Wait for that time near Legion NPCs -> the event starts on the minute with a start line in chat.",
            "After 30 minutes -> an end line shows and the Legion are back to normal.",
        ],
        "mostWanted": "Did it start at the time you set, on the server's clock?",
    },
    {
        "id": "N-04", "status": "new", "area": "Faction empowerment",
        "title": "Bandits take revenge for a fallen V Blood leader",
        "summary": "'Bandit vengeance' empowers all bandits for 10 minutes when one of the bandit V Blood leaders "
                   "dies, with a 30-minute cooldown.",
        "needs": "Admin rights to set it up; a character able to kill a bandit V Blood (Rufus the Foreman, Errol the "
                 "Stonebreaker or Quincey the Bandit King).",
        "howToTest": [
            "Type .nyar template use bandit-vengeance, .nyar pillar empowerment on, .nyar event enable "
            "bandit-vengeance.",
            "Kill one of the bandit V Bloods in the normal way -> right after, a line in chat says the bandits "
            "swear vengeance.",
            "Fight bandits at a nearby camp during the next 10 minutes -> they hit and attack faster than usual.",
            "Kill a second bandit V Blood within 30 minutes -> no second vengeance starts (cooldown).",
        ],
        "mostWanted": "Did the vengeance feel like a consequence of the kill, or like an unrelated buff?",
    },
    {
        "id": "N-05", "status": "new", "area": "Spawn events",
        "title": "An ambush waiting at a spot you choose",
        "summary": "The admin picks the place first and can then start the event from anywhere. 'Bandit ambush' "
                   "brings 3 waves of bandits to the saved spot, a minute apart.",
        "needs": "Admin rights; one or two players with gear level 25 or more; a road or clearing away from any castle.",
        "howToTest": [
            "Type .nyar template use bandit-ambush and .nyar pillar spawns on -> both confirm.",
            "Walk to where the ambush should happen and type .nyar event set bandit-ambush location here -> it "
            "confirms with your position.",
            "Set the difficulty with .nyar event set bandit-ambush action.modifiers.level 30, then type .nyar event "
            "enable bandit-ambush -> each confirms.",
            "Walk at least 100 m away and type .nyar event start bandit-ambush -> nothing appears around you; the "
            "bandits are waiting at the saved spot.",
            "Go back and fight -> waves 2 and 3 arrive a minute apart, all at level 30. When it ends (10 minutes, or "
            ".nyar event stop bandit-ambush) the leftovers disappear about 30 s later; .nyar status reads 'tracked "
            "units: 0'.",
        ],
        "mostWanted": "Were the bandits at the saved spot, on walkable ground, and at the level you set?",
    },
    {
        "id": "N-06", "status": "new", "area": "Spawn events",
        "title": "Graveyard waves: three waves, a guardian and a scoreboard",
        "summary": "'Graveyard waves' sends three growing waves of armoured skeletons at a random online player, each "
                   "wave after the last is cleared, ending with an Undead Guardian. A scoreboard ranks the players.",
        "needs": "Admin rights; two players (gear level 25 or more) is best, one works.",
        "howToTest": [
            "Type .nyar template use graveyard-waves, .nyar event enable graveyard-waves.",
            "Everyone goes to a graveyard (outside castles); the admin types .nyar event start graveyard-waves -> "
            "skeletons appear 20-40 m from one player and come for you.",
            "Clear wave 1 -> wave 2 comes (stronger), then wave 3 with the Undead Guardian.",
            "Clear wave 3 -> chat says all waves are defeated and shows the top 3 by kills with deaths.",
            "Compare the scoreboard with who actually got the kills.",
        ],
        "mostWanted": "Was the scoreboard right about who killed what?",
        "deliberate": "Admins are left off the scoreboard unless the server turns that on.",
    },
    {
        "id": "N-07", "status": "new", "area": "Spawn events",
        "title": "Hunters that roam on their own (a random interval) and split up between players",
        "summary": "'Roaming hunters' starts by itself every 60-90 minutes, sending a bandit squad at up to 3 players "
                   "who are at least 150 m apart. Here the interval is shortened to 5 minutes.",
        "needs": "Admin rights; two players at least 150 m apart (three is better).",
        "howToTest": [
            "Type .nyar template use roaming-hunters, .nyar event enable roaming-hunters.",
            "Type .nyar event set roaming-hunters trigger.minMinutes 5 and .nyar event set roaming-hunters "
            "trigger.maxMinutes 5; .nyar event info roaming-hunters shows the next start.",
            "Both players stand 150 m or more apart, outside castles -> within about 5 minutes each of you gets a "
            "separate bandit squad 20-40 m away that hunts you.",
            "One player stands inside their castle territory at the next start -> that player gets no squad.",
        ],
        "mostWanted": "Did each of you get your own squad, and did the one in a castle get none?",
    },
    {
        "id": "N-08", "status": "new", "area": "Spawn events",
        "title": "The border watch rides out when you enter Dunley Farmlands",
        "summary": "'Border watch' sends a militia patrol at a player who walks into Dunley Farmlands, at most once "
                   "every 30 minutes per player.",
        "needs": "Admin rights; a character that can survive militia; start outside Dunley Farmlands (e.g. in "
                 "Farbane Woods near the border).",
        "howToTest": [
            "Type .nyar template use border-watch, .nyar event enable border-watch.",
            "Walk across the border into Dunley Farmlands -> within seconds militia appear 20-40 m away and hunt you.",
            "Type .nyar region here -> it names Dunley Farmlands.",
            "Leave and walk back in within 30 minutes -> nothing spawns the second time.",
        ],
        "mostWanted": "Did the patrol feel like a reaction to crossing the border?",
    },
    {
        "id": "N-09", "status": "new", "area": "Spawn events",
        "title": "Bandit reprisal after a killing spree",
        "summary": "'Bandit reprisal' sends a squad when one player kills many bandits in a short time. Set here to "
                   "10 kills in 10 minutes, a realistic setting for a small server.",
        "needs": "Admin rights; a bandit camp with at least 10 bandits.",
        "howToTest": [
            "Type .nyar template use bandit-reprisal, .nyar event enable bandit-reprisal.",
            "Type .nyar event set bandit-reprisal trigger.kills 10 and .nyar event set bandit-reprisal "
            "trigger.windowSeconds 600.",
            "Kill 10 bandits within 10 minutes -> right after the tenth, a squad appears 20-40 m away and hunts you.",
            "A second player kills only 5 bandits -> no squad comes for them.",
        ],
        "mostWanted": "Did the squad come for the right player?",
    },
    {
        "id": "N-10", "status": "new", "area": "Swarm",
        "title": "A scheduled arena night: the spider swarm at a set place and time",
        "summary": "How a server runs a weekly event: the admin picks the arena, sets the day and time and tells the "
                   "players. The swarm then starts by itself: 5 growing waves in the arena, the Spider Queen last, and "
                   "the best kills-to-deaths ratio wins.",
        "needs": "Admin rights; the server's local time; two players with gear level 35 or more (the Queen is level "
                 "45); open ground away from castles.",
        "howToTest": [
            "Type .nyar template use spider-swarm. Stand in the middle of your arena and type .nyar event set "
            "spider-swarm location here -> it confirms; the arena is the 40 m around that spot.",
            "Type .nyar event set spider-swarm trigger.type Schedule, .nyar event set spider-swarm trigger.days "
            "<today, e.g. Sat> and .nyar event set spider-swarm trigger.times <server time plus 10 minutes, e.g. "
            "21:40>, then .nyar event enable spider-swarm -> .nyar event info spider-swarm shows the next start and "
            "all five waves.",
            "Tell everyone: .nyar announce Spider arena at <place> in 10 minutes -> every player sees the line.",
            "Gather in the arena; nobody types start -> on the minute, a start line shows and spiders appear in the "
            "arena.",
            "Run more than 40 m out during a wave -> the spiders do not follow. Clear wave 5 and the Queen -> the "
            "scoreboard names a winner. Then type .nyar event disable spider-swarm so it does not repeat next week.",
        ],
        "mostWanted": "Could you run a real arena night this way, and did the arena edge feel fair?",
    },
    {
        "id": "N-11", "status": "new", "area": "Hunting grounds",
        "title": "Spider hunting grounds: spiders roam a whole region and hunt whoever is inside",
        "summary": "'Spider hunting grounds' spreads spider groups over Dunley Farmlands; they hunt players inside the "
                   "region and never chase anyone out of it. The last wave brings the Spider Queen.",
        "needs": "Admin rights; two players, one inside Dunley Farmlands and one just outside it.",
        "howToTest": [
            "Type .nyar template use spider-hunting-grounds, .nyar event enable spider-hunting-grounds, .nyar event "
            "start spider-hunting-grounds.",
            "The player inside Dunley waits in the open -> within a few minutes spider groups find them.",
            "That player runs out of the region across the border -> the spiders stop at the border and turn back.",
            "The player outside the region stays outside -> no spiders come for them.",
            "Watch spiders approach from far away -> you never see one appear out of nowhere close to you.",
        ],
        "mostWanted": "Did you ever see a spider pop in or jump while you were watching?",
        "deliberate": "Far from players the spiders close in by hidden jumps; never within 100 m of a player.",
    },
    {
        "id": "N-12", "status": "new", "area": "Hunting grounds",
        "title": "A regional ambush: only players inside the region are picked",
        "summary": "An admin turns the graveyard waves into an ambush that only hits players in Farbane Woods, and keeps "
                   "the undead from chasing them out. This is set up from chat, as an admin would on a live server.",
        "needs": "Admin rights; two players, one in Farbane Woods and one in another region.",
        "howToTest": [
            "Type .nyar template use graveyard-waves as farbane-ambush, then .nyar event enable farbane-ambush.",
            "Type .nyar event set farbane-ambush action.scope FarbaneWoods and .nyar event set farbane-ambush "
            "action.regionLock true -> each confirms.",
            "With one player in Farbane Woods and one elsewhere, type .nyar event start farbane-ambush -> the "
            "skeletons appear only near the player in Farbane Woods.",
            "That player runs out of Farbane Woods -> the skeletons do not follow across the border.",
        ],
        "mostWanted": "Were the chat commands clear enough to set this up without the documentation?",
    },
    {
        "id": "N-13", "status": "new", "area": "Revenge",
        "title": "Bandit revenge: warnings, then a private hunt",
        "summary": "Kill enough bandits and they swear revenge on you alone: private warnings as their anger grows, a "
                   "server alert that names no one, then three waves that hunt only you.",
        "needs": "Admin rights; a bandit camp; ideally a second player nearby who is not the target.",
        "howToTest": [
            "Type .nyar template use bandit-revenge, .nyar event enable bandit-revenge.",
            "Type .nyar event set bandit-revenge trigger.warnAt 4,8 first, then .nyar event set bandit-revenge "
            "trigger.heat 12 (a small-server setting; the warnings must stay below the heat, so this order) -> each "
            "confirms.",
            "Kill bandits -> after 4 and after 8 kills you get a private warning; the other player sees nothing.",
            "Kill 12 -> everyone sees an alert that names no one; you get a private line; bandits appear 20-40 m "
            "from you and hunt you.",
            "The second player stands near you -> the bandits keep going after you, not them.",
            "Clear all three waves -> you get a private end line.",
        ],
        "mostWanted": "Did the warnings give you a fair chance to stop before the revenge started?",
    },
    {
        "id": "N-14", "status": "new", "area": "Apocalypse",
        "title": "Zombie apocalypse: a two-hour outbreak the server has to survive",
        "summary": "The undead rise at three hotspots, roamers hunt players across the map and spread with every kill, "
                   "and four hordes march on a place the admin names for the server to defend. The last horde brings "
                   "the Bishop of Death.",
        "needs": "Admin rights; two hours; as many players as you can gather, gear level 30 or more; a place to defend "
                 "outside any castle (a crossroads, a ruin).",
        "howToTest": [
            "Type .nyar template use zombie-apocalypse and .nyar event set zombie-apocalypse durationSeconds 7200.",
            "Walk to the place to defend and type .nyar event set zombie-apocalypse action.goal here, then .nyar "
            "event set zombie-apocalypse action.goal.name Crossroads, then .nyar event enable zombie-apocalypse -> "
            "each confirms.",
            "When everyone is ready, type .nyar event start zombie-apocalypse from anywhere -> chat says the dead "
            "rise; .nyar event info zombie-apocalypse lists three hotspots and the defended place.",
            "Players spread out over the map -> within a few minutes roamers find players far from the defended place.",
            "After 10 minutes, then every 30 -> chat announces each horde rising and its march towards the place.",
            "Hold the place, or stand in a horde's path -> it stops to fight while you hold within about 60 m of it. "
            "Play it out -> chat shows the outcome (won, lost or survived) and the scoreboard.",
        ],
        "mostWanted": "Did the outbreak feel like a server-wide event worth gathering for?",
        "knownLimit": "Hordes count as one group: a straggler left far from everyone keeps its horde alive until found.",
    },
    {
        "id": "N-15", "status": "new", "area": "Unit watch",
        "title": "A monster that cannot reach you is moved, and a wave never hangs",
        "summary": "If a spawned monster wants a player but cannot reach them for 90 seconds, the server moves it out of "
                   "sight; after two tries it removes it. Here a player stands where spiders cannot climb.",
        "needs": "Admin rights; a tall rock or ledge a spider cannot climb, or a small island across deep water.",
        "howToTest": [
            "Stand at the foot of the rock. Type .nyar template use spider-swarm as stuck-test, .nyar event enable "
            "stuck-test, .nyar event start stuck-test.",
            "As soon as spiders appear, climb onto the rock and stay within about 20 m of them without attacking.",
            "Wait about 2 minutes -> type .nyar event info stuck-test: the unit watch line reads 'stuck now' above 0.",
            "Walk 150 m away for a minute, then come back -> the stuck spiders are no longer at the foot of the rock.",
            "While watching any spider -> none vanishes or appears in front of you.",
        ],
        "mostWanted": "Did anything vanish or appear while you were looking at it?",
        "deliberate": "A spider that still cannot reach anyone after two moves is removed with no kill credit.",
    },
    {
        "id": "N-16", "status": "new", "area": "Castles",
        "title": "Castles stay safe: no spawns or jumps inside claimed territory",
        "summary": "Events never put monsters inside a claimed castle and never pick a player standing in their castle.",
        "needs": "Admin rights; a claimed castle; a second player outside it is best.",
        "howToTest": [
            "Stand inside your castle territory with nobody else online outside one. Type .nyar event start "
            "graveyard-waves (set up in N-06) -> no skeletons come for you: a player in their castle is never picked.",
            "Start roaming-hunters (N-07) while one player is in their castle and one outside -> only the outside "
            "player gets a squad.",
            "During the apocalypse (N-14), shelter in the castle -> roamers do not appear inside it.",
        ],
        "mostWanted": "Did any monster ever end up inside castle territory?",
    },
    {
        "id": "N-17", "status": "new", "area": "Admin safety",
        "title": "Stopping things: event stop, switching a kind off, and the emergency stop",
        "summary": "Admins must always be able to stop everything. Three ways, from gentle to emergency.",
        "needs": "Admin rights; two events from earlier rows.",
        "howToTest": [
            "Start graveyard-waves, then type .nyar event stop graveyard-waves -> the scoreboard shows; the "
            "skeletons disappear within about a minute; .nyar status reads 'tracked units: 0'.",
            "Start bandit-ambush, then type .nyar pillar spawns off -> the ambush ends and its bandits disappear.",
            "Type .nyar pillar spawns on, start two spawn events, then type .nyar purge and .nyar purge confirm -> "
            "everything ends and every spawned monster is gone within a minute; nothing is shown in chat.",
            "Try .nyar event start bandit-ambush right after -> it is refused for about 4 minutes.",
        ],
        "mostWanted": "Was anything left behind after any of the three?",
    },
    {
        "id": "N-18", "status": "new", "area": "Spawn events",
        "title": "Timed waves at a muster point: the next wave comes whether you are ready or not",
        "summary": "'Militia muster (timed)' brings 5 waves of militia to a place the admin saves, a new wave every 75 "
                   "seconds even if the last is still alive, ending on the militia leader. Six other factions have a "
                   "timed version too (names ending in -timed).",
        "needs": "Admin rights; the Spawns kind on (.nyar pillar spawns on); two players with gear level 50 or more "
                 "(the militia leader is level 58); open ground in Dunley Farmlands.",
        "howToTest": [
            "Type .nyar template use militia-muster-timed. At an open spot in Dunley Farmlands type .nyar event set "
            "militia-muster-timed location here -> it confirms.",
            "Give it a time limit: .nyar event set militia-muster-timed durationSeconds 900 (15 minutes), then .nyar "
            "event enable militia-muster-timed.",
            "With the players at the spot, type .nyar event start militia-muster-timed -> wave 1 (6 militia) appears "
            "there.",
            "Hold back -> about 75 seconds after the start, wave 2 (9 militia) arrives while wave 1 is still alive.",
            "Fight on -> a new wave every 75 seconds; wave 5 brings the militia leader; the scoreboard names a winner. "
            "If you are still fighting at 15 minutes, the event ends.",
        ],
        "mostWanted": "Did the pressure of the clock feel exciting or simply unfair?",
    },
    {
        "id": "N-19", "status": "new", "area": "Spawn events",
        "title": "Onslaught: random waves, surprise bosses, and no running away",
        "summary": "'Undead onslaught' rolls each wave at random from groups of undead, adds a random undead V Blood in "
                   "the middle wave and one or two at the end. If nobody stays in its 40 m arena, after a set time the "
                   "undead go after the nearest player anywhere on the map. Six other factions have one too (names "
                   "ending in -onslaught).",
        "needs": "Admin rights; the Spawns kind on; two players with gear level 60 or more (the undead V Bloods run "
                 "from level 27 to 63, up to two at once); a castle within reach.",
        "howToTest": [
            "Type .nyar template use undead-onslaught. At an open spot near a graveyard type .nyar event set "
            "undead-onslaught location here.",
            "Set how long they wait before giving chase: .nyar event set undead-onslaught action.pursuit 90, then "
            ".nyar event enable undead-onslaught and .nyar event start undead-onslaught -> about 12 undead appear "
            "at the spot.",
            "Clear wave 1 or wait 75 seconds -> wave 2 brings different undead and one V Blood boss.",
            "Both players run more than 60 m away and wait -> about 90 seconds later the undead come after the "
            "nearest of you, arriving near you rather than on top of you. Go into your castle's territory -> they "
            "do not follow you in.",
            "Stop it (.nyar event stop undead-onslaught) and start it again -> the waves and bosses are different.",
        ],
        "mostWanted": "Did the chase feel like a threat, and did anything visibly teleport while you watched?",
    },
    {
        "id": "E-01", "status": "explore", "area": "Spawn events",
        "title": "Build your own event from chat, the way a new admin would",
        "summary": "Without the documentation, create a new spawn event from scratch with chat commands, set it up as "
                   "you would for your own server, and run it.",
        "needs": "Admin rights; 20 minutes.",
        "howToTest": [
            "Type .nyar event new my-event spawns -> a switched-off event is created.",
            "Use .nyar event set my-event ... to choose units (action.units, e.g. CHAR_Forest_Wolf:4), waves, the "
            "location (location here) and a level (action.modifiers.level 30).",
            "Type .nyar event info my-event, then enable and start it -> it runs as you set it.",
            "Note every reply that confused you, word for word.",
        ],
        "mostWanted": "Which reply or field name stopped you?",
        "weCouldNotVerify": "Our tests check that every command is accepted or refused correctly; they cannot tell "
                            "whether a person can find their way through them.",
    },
    {
        "id": "E-02", "status": "explore", "area": "Two players",
        "title": "A second player joins someone else's fight",
        "summary": "Take any event from the rows above that hunts one player (revenge, the border watch) and have a "
                   "second player join in from the side.",
        "needs": "Two players at the same time.",
        "howToTest": [
            "Player one triggers N-08 or N-13 -> monsters hunt player one.",
            "Player two joins the fight and attacks the monsters -> note who they turn on, and who gets kill credit.",
        ],
        "mostWanted": "Did the fight still make sense with a second player in it?",
        "weCouldNotVerify": "Our tool plays one character; it has never seen two players in one event.",
    },
    {
        "id": "C-01", "status": "check", "area": "Server feel",
        "title": "Measurement: how the server feels during the busiest event",
        "summary": "Not a pass or fail row. During the apocalypse (N-14), note any lag, rubber-banding or slow "
                   "abilities, and when it happened. One reading is a baseline, not a series.",
        "needs": "Being in the apocalypse run of N-14.",
        "howToTest": [
            "At the start, at each horde rise and at the end, note how responsive the game feels (fine, slight lag, "
            "bad lag) and the time.",
            "If something felt bad, write what you were doing and how many monsters were around.",
        ],
        "mostWanted": "Was there any moment the server felt slower than usual, and when?",
        "deliberate": "This row is not pass or fail; 'did not notice anything' is an accepted answer.",
    },
]
