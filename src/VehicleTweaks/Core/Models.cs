using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VehicleTweaks.Core
{
    /// <summary>
    /// Every vehicle model in the game, by the name the files call it.
    ///
    /// IN CORE, BECAUSE TWO THINGS WANT IT NOW. The spawner builds its catalogue from this,
    /// and the stance uses it to turn the car you are sitting in back into a name it can
    /// write in a file. A list of strings is data, not a menu.
    ///
    /// NOT SHVDN'S ENUMERATION, AND THAT IS THE WHOLE POINT OF THE FILE. GTA.VehicleHash is
    /// frozen at whatever the wrapper last shipped -- 843 names, 841 of them distinct, and not
    /// one thing added by a game update since. Eighty vehicles were missing from the spawner
    /// because of it: the whole of the 2024 and 2025 packs, the drift cars, the Christmas 2023
    /// additions. A spawner that cannot spawn the car somebody just bought has a hole in it,
    /// and the hole was in the list rather than in the code.
    ///
    /// NAMES RATHER THAN HASHES, because a name is the only part of this a person can use. It
    /// is what the MODEL row shows, what the picture is filed under, and what every other ini,
    /// trainer and add-on readme calls the car. Model hashes it with the game's own joaat, so
    /// nothing here has to agree with an enumeration to work.
    ///
    /// WHAT IS INSTALLED IS STILL THE GAME'S ANSWER, NOT THIS LIST'S. Every name goes through
    /// Model.IsInCdImage before it is offered, so a Legacy install without the newest packs
    /// simply shows fewer -- which is also why this list is allowed to be ahead of the game
    /// without the menu ever lying about what it can spawn.
    ///
    /// AND IT IS NOT THE ONLY SOURCE, BECAUSE IT CANNOT BE. A shipped list is a photograph: it
    /// goes stale the day Rockstar ships a pack, and it never knew about the add-on cars somebody
    /// dropped into dlcpacks in the first place. Found() reads the machine as well -- see there
    /// -- and everything it turns up is put through the same IsInCdImage as everything here.
    ///
    /// The 935 names are the community's dump of the game's own vehicle metadata, each one
    /// checked here against the hash that dump records for it before it was written out.
    /// </summary>
    internal static class Models
    {
        public static readonly string[] All =
        {
            "adder", "airbus", "airtug", "akula", "akuma", "aleutian",
            "alkonost", "alpha", "alphaz1", "ambulance", "annihilator", "annihilator2",
            "apc", "arbitergt", "ardent", "armytanker", "armytrailer", "armytrailer2",
            "asbo", "asea", "asea2", "asterope", "asterope2", "astrale",
            "astron", "astron2", "autarch", "avarus", "avenger", "avenger2",
            "avenger3", "avenger4", "avisa", "bagger", "baletrailer", "baller",
            "baller2", "baller3", "baller4", "baller5", "baller6", "baller7",
            "baller8", "banshee", "banshee2", "banshee3", "barracks", "barracks2",
            "barracks3", "barrage", "bati", "bati2", "benson", "benson2",
            "besra", "bestiagts", "bf400", "bfinjection", "biff", "bifta",
            "bison", "bison2", "bison3", "bjxl", "blade", "blazer",
            "blazer2", "blazer3", "blazer4", "blazer5", "blimp", "blimp2",
            "blimp3", "blista", "blista2", "blista3", "bmx", "boattrailer",
            "boattrailer2", "boattrailer3", "bobcatxl", "bodhi2", "bombushka", "boor",
            "boxville", "boxville2", "boxville3", "boxville4", "boxville5", "boxville6",
            "brawler", "brickade", "brickade2", "brigham", "brioso", "brioso2",
            "brioso3", "broadway", "bruiser", "bruiser2", "bruiser3", "brutus",
            "brutus2", "brutus3", "btype", "btype2", "btype3", "buccaneer",
            "buccaneer2", "buffalo", "buffalo2", "buffalo3", "buffalo4", "buffalo5",
            "bulldozer", "bullet", "burrito", "burrito2", "burrito3", "burrito4",
            "burrito5", "bus", "buzzard", "buzzard2", "cablecar", "caddy",
            "caddy2", "caddy3", "calico", "camper", "caracara", "caracara2",
            "caracara3", "carbonizzare", "carbonrs", "cargobob", "cargobob2", "cargobob3",
            "cargobob4", "cargobob5", "cargoplane", "cargoplane2", "cartuccia", "casco",
            "castigator", "cavalcade", "cavalcade2", "cavalcade3", "cerberus", "cerberus2",
            "cerberus3", "champion", "chavosv6", "cheburek", "cheetah", "cheetah2",
            "cheetah3", "chernobog", "chimera", "chino", "chino2", "cinquemila",
            "cliffhanger", "clique", "clique2", "club", "coach", "cog55",
            "cog552", "cogcabrio", "cognoscenti", "cognoscenti2", "comet2", "comet3",
            "comet4", "comet5", "comet6", "comet7", "conada", "conada2",
            "contender", "coquette", "coquette2", "coquette3", "coquette4", "coquette5",
            "coquette6", "corsita", "coureur", "cruiser", "crusader", "cuban800",
            "cutter", "cyclone", "cyclone2", "cypher", "daemon", "daemon2",
            "deathbike", "deathbike2", "deathbike3", "defiler", "deity", "deluxo",
            "deveste", "deviant", "diablous", "diablous2", "dilettante", "dilettante2",
            "dinghy", "dinghy2", "dinghy3", "dinghy4", "dinghy5", "dloader",
            "docktrailer", "docktug", "dodo", "dominator", "dominator10", "dominator2",
            "dominator3", "dominator4", "dominator5", "dominator6", "dominator7", "dominator8",
            "dominator9", "dorado", "double", "drafter", "draugur", "driftchavosv6",
            "driftcheburek", "driftcoquette", "driftcypher", "driftdominator10", "driftdominator8", "driftdominator9",
            "driftelegy", "drifteuros", "driftfr36", "driftfuto", "driftfuto2", "driftgauntlet4",
            "drifthardy", "driftjester", "driftjester3", "driftkeitora", "driftl352", "driftnebula",
            "driftremus", "driftrt3000", "driftsentinel", "driftsentinel2", "drifttampa", "driftvorschlag",
            "driftyosemite", "driftzr350", "dubsta", "dubsta2", "dubsta3", "dukes",
            "dukes2", "dukes3", "dump", "dune", "dune2", "dune3",
            "dune4", "dune5", "duster", "duster2", "dynasty", "elegy",
            "elegy2", "ellie", "emerus", "emperor", "emperor2", "emperor3",
            "enduro", "entity2", "entity3", "entityxf", "envisage", "esskey",
            "estride", "eudora", "euros", "eurosx32", "everon", "everon2",
            "everon3", "exemplar", "f620", "faction", "faction2", "faction3",
            "fagaloa", "faggio", "faggio2", "faggio3", "fbi", "fbi2",
            "fcr", "fcr2", "felon", "felon2", "feltzer2", "feltzer3",
            "firebolt", "firetruk", "fixter", "flashgt", "flatbed", "flatbed2",
            "fmj", "fmj2", "forklift", "formula", "formula2", "fq2",
            "fr36", "freecrawler", "freight", "freight2", "freightcar", "freightcar2",
            "freightcar3", "freightcont1", "freightcont2", "freightgrain", "freighttrailer", "frogger",
            "frogger2", "fugitive", "furia", "furoregt", "fusilade", "futo",
            "futo2", "gargoyle", "gauntlet", "gauntlet2", "gauntlet3", "gauntlet4",
            "gauntlet5", "gauntlet6", "gb200", "gburrito", "gburrito2", "glendale",
            "glendale2", "gp1", "graintrailer", "granger", "granger2", "greenwood",
            "gresley", "growler", "gt500", "gt750", "guardian", "habanero",
            "hakuchou", "hakuchou2", "halftrack", "handler", "hardy", "hauler",
            "hauler2", "havok", "hellion", "hermes", "hexer", "horus",
            "hotknife", "hotring", "howard", "hunter", "huntley", "hustler",
            "hydra", "ignus", "ignus2", "imorgon", "impaler", "impaler2",
            "impaler3", "impaler4", "impaler5", "impaler6", "imperator", "imperator2",
            "imperator3", "inductor", "inductor2", "infernus", "infernus2", "ingot",
            "innovation", "insurgent", "insurgent2", "insurgent3", "intruder", "issi2",
            "issi3", "issi4", "issi5", "issi6", "issi7", "issi8",
            "itali2", "italigtb", "italigtb2", "italigto", "italirsx", "iwagen",
            "jackal", "jb700", "jb7002", "jester", "jester2", "jester3",
            "jester4", "jester5", "jet", "jetmax", "journey", "journey2",
            "jubilee", "jugular", "kalahari", "kamacho", "kanjo", "kanjosj",
            "keitora", "khamelion", "khanjali", "komoda", "kosatka", "krieger",
            "kuruma", "kuruma2", "l35", "l352", "landstalker", "landstalker2",
            "laufer", "lazer", "le7b", "lectro", "lguard", "limo2",
            "lm87", "locust", "longfin", "lrcgt", "luiva", "lurcher",
            "luxor", "luxor2", "lynx", "mamba", "mammatus", "manana",
            "manana2", "manchez", "manchez2", "manchez3", "marquis", "marshall",
            "massacro", "massacro2", "maverick", "maverick2", "menacer", "merula",
            "mesa", "mesa2", "mesa3", "metrotrain", "michelli", "microlight",
            "miljet", "minimus", "minitank", "minivan", "minivan2", "mixer",
            "mixer2", "mogul", "molotok", "monroe", "monster", "monster3",
            "monster4", "monster5", "monstrociti", "moonbeam", "moonbeam2", "mower",
            "mule", "mule2", "mule3", "mule4", "mule5", "nebula",
            "nemesis", "neo", "neon", "nero", "nero2", "nightblade",
            "nightshade", "nightshark", "nimbus", "ninef", "ninef2", "niobe",
            "nokota", "novak", "omnis", "omnisegt", "openwheel1", "openwheel2",
            "oppressor", "oppressor2", "oracle", "oracle2", "osiris", "outlaw",
            "packer", "panthere", "panto", "paradise", "paragon", "paragon2",
            "paragon3", "pariah", "patriot", "patriot2", "patriot3", "patrolboat",
            "pbus", "pbus2", "pcj", "penetrator", "penumbra", "penumbra2",
            "peyote", "peyote2", "peyote3", "pfister811", "phantom", "phantom2",
            "phantom3", "phantom4", "phoenix", "picador", "pigalle", "pipistrello",
            "pizzaboy", "polbuffalo", "polbuffalo6", "polcaracara", "polcoquette4", "poldominator10",
            "poldorado", "polfaction2", "polgauntlet", "polgreenwood", "police", "police2",
            "police3", "police4", "police5", "policeb", "policeb2", "policeold1",
            "policeold2", "policet", "policet3", "polignus", "polimpaler5", "polimpaler6",
            "polmav", "polterminus", "pony", "pony2", "postlude", "pounder",
            "pounder2", "powersurge", "prairie", "pranger", "predator", "premier",
            "previon", "primo", "primo2", "proptrailer", "prototipo", "pyro",
            "r300", "radi", "raiden", "raiju", "raketrailer", "rallytruck",
            "rancherxl", "rancherxl2", "rapidgt", "rapidgt2", "rapidgt3", "rapidgt4",
            "raptor", "ratbike", "ratel", "ratloader", "ratloader2", "rcbandito",
            "reaper", "rebel", "rebel2", "rebla", "reever", "regina",
            "remus", "rentalbus", "retinue", "retinue2", "revolter", "rhapsody",
            "rhinehart", "rhino", "riata", "riot", "riot2", "ripley",
            "rocoto", "rogue", "romero", "rrocket", "rt3000", "rubble",
            "ruffian", "ruiner", "ruiner2", "ruiner3", "ruiner4", "rumpo",
            "rumpo2", "rumpo3", "ruston", "s80", "s95", "sabregt",
            "sabregt2", "sadler", "sadler2", "sanchez", "sanchez2", "sanctus",
            "sandking", "sandking2", "savage", "savestra", "sc1", "scarab",
            "scarab2", "scarab3", "schafter2", "schafter3", "schafter4", "schafter5",
            "schafter6", "schlagen", "schwarzer", "scorcher", "scramjet", "scrap",
            "seabreeze", "seashark", "seashark2", "seashark3", "seasparrow", "seasparrow2",
            "seasparrow3", "seminole", "seminole2", "sentinel", "sentinel2", "sentinel3",
            "sentinel4", "sentinel5", "sentinel6", "serrano", "seven70", "shamal",
            "sheava", "sheriff", "sheriff2", "shinobi", "shotaro", "skylift",
            "slamtruck", "slamvan", "slamvan2", "slamvan3", "slamvan4", "slamvan5",
            "slamvan6", "sm722", "sovereign", "specter", "specter2", "speeder",
            "speeder2", "speedo", "speedo2", "speedo4", "speedo5", "squaddie",
            "squalo", "stafford", "stalion", "stalion2", "stanier", "starling",
            "stinger", "stingergt", "stingertt", "stockade", "stockade3", "stockade4",
            "stratum", "streamer216", "streiter", "stretch", "strikeforce", "stromberg",
            "stryder", "stunt", "submersible", "submersible2", "sugoi", "sultan",
            "sultan2", "sultan3", "sultanrs", "suntrap", "superd", "supervolito",
            "supervolito2", "surano", "surfer", "surfer2", "surfer3", "surge",
            "suzume", "swift", "swift2", "swinger", "t20", "taco",
            "tahoma", "tailgater", "tailgater2", "taipan", "tampa", "tampa2",
            "tampa3", "tampa4", "tanker", "tanker2", "tankercar", "taxi",
            "technical", "technical2", "technical3", "tempesta", "tenf", "tenf2",
            "terbyte", "terminus", "tezeract", "thrax", "thrust", "thruster",
            "tigon", "tiptruck", "tiptruck2", "titan", "titan2", "toreador",
            "torero", "torero2", "tornado", "tornado2", "tornado3", "tornado4",
            "tornado5", "tornado6", "toro", "toro2", "toros", "tourbus",
            "towtruck", "towtruck2", "towtruck3", "towtruck4", "tr2", "tr3",
            "tr4", "tractor", "tractor2", "tractor3", "trailerlarge", "trailerlogs",
            "trailers", "trailers2", "trailers3", "trailers4", "trailers5", "trailersmall",
            "trailersmall2", "trash", "trash2", "trflat", "trflat2", "tribike",
            "tribike2", "tribike3", "trophytruck", "trophytruck2", "tropic", "tropic2",
            "tropos", "tug", "tula", "tulip", "tulip2", "turismo2",
            "turismo3", "turismor", "tvtrailer", "tvtrailer2", "tyrant", "tyrus",
            "uranus", "utillitruck", "utillitruck2", "utillitruck3", "vacca", "vader",
            "vagner", "vagrant", "valkyrie", "valkyrie2", "vamos", "vectre",
            "velenogt", "velum", "velum2", "verlierer2", "verus", "vestra",
            "vetir", "veto", "veto2", "vigero", "vigero2", "vigero3",
            "vigilante", "vindicator", "virgo", "virgo2", "virgo3", "virtue",
            "viseris", "visione", "vivanite", "vivanite2", "volatol", "volatus",
            "voltic", "voltic2", "voodoo", "voodoo2", "vorschlaghammer", "vortex",
            "vstr", "warden", "warrener", "warrener2", "washington", "wastelander",
            "weevil", "weevil2", "windsor", "windsor2", "winky", "wolfsbane",
            "woodlander", "xa21", "xls", "xls2", "xtreme", "yosemite",
            "yosemite1500", "yosemite2", "yosemite3", "youga", "youga2", "youga3",
            "youga4", "youga5", "z190", "zeno", "zentorno", "zhaba",
            "zion", "zion2", "zion3", "zombiea", "zombieb", "zorrusso",
            "zr350", "zr380", "zr3802", "zr3803", "ztype",
        };

        /// <summary>
        /// Names read off THIS machine, on top of the ones this file ships with.
        ///
        /// A SHIPPED LIST IS A PHOTOGRAPH. It goes stale the day Rockstar ships a pack -- the
        /// fourteen above were found that way, months after the dump they came from was cut --
        /// and it never knew about the add-on cars somebody dropped into dlcpacks in the first
        /// place. Neither of those is a reason to hand-maintain a list forever; they are a reason
        /// to look at the machine the mod is running on.
        ///
        /// THREE PLACES, IN ORDER OF HOW MUCH THEY KNOW:
        ///
        /// 1. ADD-ON VEHICLE SPAWNER'S CACHE, if that mod is installed beside this one. It finds
        ///    vehicles by hooking the game's own archetype loader, which a script cannot do, and
        ///    writes what it found to AddonSpawner\hashes.cache as plain "hash name" lines. That
        ///    is somebody else's work being left where anyone can read it, and reading it is the
        ///    single best source of add-on names there is. Absent, and nothing is lost.
        ///
        /// 2. THE DLCPACK FOLDER NAMES themselves. An add-on car's pack is very often named after
        ///    the car in it -- c63s, focusrs, hellcat, m3g80 -- so the folder list is a free pile
        ///    of good guesses. The ones that are not vehicles cost a failed lookup each.
        ///
        /// 3. A LIST YOU KEEP YOURSELF, one name per line, beside the log. For the add-on whose
        ///    pack is called something else entirely, which no amount of reading can work out.
        ///
        /// NONE OF IT IS TRUSTED. Every name from every source goes through the same
        /// Model.IsInCdImage as the shipped ones, so a wrong guess is a lookup that fails and
        /// nothing else. That is what makes it safe to guess at all.
        /// </summary>
        public static string[] Found(string game, string list)
        {
            var names = new List<string>();

            Cache(names, Path.Combine(game, "AddonSpawner", "hashes.cache"));

            Packs(names, Path.Combine(game, "mods", "update", "x64", "dlcpacks"));
            Packs(names, Path.Combine(game, "update", "x64", "dlcpacks"));

            Lines(names, list);

            return names.ToArray();
        }

        /// <summary>Add-On Vehicle Spawner's cache: "hash name", one a line.</summary>
        private static void Cache(List<string> names, string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 2) names.Add(parts[1]);
                }
            }
            catch (Exception ex)
            {
                Log.Once("models-cache", "Could not read the add-on spawner's cache: " + ex.Message);
            }
        }

        /// <summary>Every folder in a dlcpacks directory, on the chance it is named after its car.</summary>
        private static void Packs(List<string> names, string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;

                foreach (var folder in Directory.GetDirectories(path))
                {
                    names.Add(Path.GetFileName(folder));
                }
            }
            catch (Exception ex)
            {
                Log.Once("models-packs", "Could not read the dlcpacks folder: " + ex.Message);
            }
        }

        /// <summary>The player's own list, and the file itself if they have not made one.</summary>
        private static void Lines(List<string> names, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Write(path);
                    return;
                }

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();

                    if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                    names.Add(line);
                }
            }
            catch (Exception ex)
            {
                Log.Once("models-list", "Could not read your model list: " + ex.Message);
            }
        }

        private static void Write(string path)
        {
            try
            {
                var folder = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(path,
                    "; EXTRA VEHICLE MODELS, ONE NAME A LINE." + Environment.NewLine +
                    ";" + Environment.NewLine +
                    "; The spawner already ships the whole game and reads Add-On Vehicle Spawner's" + Environment.NewLine +
                    "; cache and your dlcpacks folder names, so most add-on cars turn up on their" + Environment.NewLine +
                    "; own. This is for the one whose pack is called something the car is not." + Environment.NewLine +
                    ";" + Environment.NewLine +
                    "; A name that is not installed simply does not appear - nothing breaks, and" + Environment.NewLine +
                    "; the log says how many of these it found." + Environment.NewLine +
                    ";" + Environment.NewLine +
                    "; sultan2" + Environment.NewLine +
                    "; mycoolcar" + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch
            {
                // A list nobody can write is a list nobody needed.
            }
        }

    }
}
