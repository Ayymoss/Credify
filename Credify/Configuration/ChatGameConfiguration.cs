namespace Credify.Configuration;

public class ChatGameConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public TimeSpan Frequency { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan CountdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MathTestTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan TriviaTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TypingTestTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan CompleteWordTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan AcronymTimeout { get; set; } = TimeSpan.FromSeconds(25);
    public int MaxPayout { get; set; } = 10_000;
    public int TypingTestTextLength { get; set; } = 10;

    /// <summary>
    /// Grace period after timeout before calculating winners.
    /// Allows time for late RCON messages to arrive.
    /// </summary>
    public TimeSpan EndGracePeriod { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Decay exponent for payout calculation. 
    /// 0.5 = square root (default, options-style theta decay)
    /// 1.0 = linear decay
    /// Lower values = more generous to slower answers
    /// </summary>
    public double PayoutDecayExponent { get; set; } = 0.5;

    public PassiveToggle EnabledPassiveGames { get; set; } = new();

    /// <summary>
    /// Acronym game word list. Key is the acronym, value is list of valid answers.
    /// </summary>
    public Dictionary<string, List<string>> Acronyms { get; set; } = new()
    {
        // Game modes
        ["FFA"] = ["Free For All"],
        ["TDM"] = ["Team Deathmatch"],
        ["DOM"] = ["Domination"],
        ["S&D"] = ["Search and Destroy", "Search & Destroy", "SnD"],
        ["CTF"] = ["Capture The Flag"],
        ["HQ"] = ["Headquarters"],
        ["KC"] = ["Kill Confirmed"],
        ["HP"] = ["Hardpoint"],
        ["GW"] = ["Ground War"],
        
        // Gameplay terms
        ["ADS"] = ["Aim Down Sights", "Aim Down Sight"],
        ["TTK"] = ["Time To Kill"],
        ["KDR"] = ["Kill Death Ratio", "Kill/Death Ratio"],
        ["SPM"] = ["Score Per Minute"],
        ["OBJ"] = ["Objective"],
        ["UAV"] = ["Unmanned Aerial Vehicle"],
        ["EMP"] = ["Electromagnetic Pulse"],
        ["RPG"] = ["Rocket Propelled Grenade"],
        ["LMG"] = ["Light Machine Gun"],
        ["SMG"] = ["Submachine Gun"],
        ["DMR"] = ["Designated Marksman Rifle"],
        ["AR"] = ["Assault Rifle"],
        
        // Slang/Community
        ["GG"] = ["Good Game"],
        ["GGWP"] = ["Good Game Well Played"],
        ["AFK"] = ["Away From Keyboard"],
        ["BRB"] = ["Be Right Back"],
        ["NPC"] = ["Non Player Character", "Non-Player Character"],
        ["DLC"] = ["Downloadable Content"],
        ["OP"] = ["Overpowered"],
        ["MVP"] = ["Most Valuable Player"],
        ["KS"] = ["Killstreak"],
        ["WP"] = ["Well Played"],
        ["GL"] = ["Good Luck"],
        ["HF"] = ["Have Fun"],
        ["GLHF"] = ["Good Luck Have Fun"],
        ["POTG"] = ["Play Of The Game"],
        ["OHK"] = ["One Hit Kill"],
        ["QS"] = ["Quickscope"],
        ["NS"] = ["Noscope", "No Scope", "Nice Shot"],
        ["HC"] = ["Hardcore"],
        ["MP"] = ["Multiplayer"],
        ["SP"] = ["Single Player", "Singleplayer"],
        ["FPS"] = ["First Person Shooter", "Frames Per Second"],
        ["PVP"] = ["Player Versus Player", "Player vs Player"],
        ["PVE"] = ["Player Versus Environment", "Player vs Environment"],
        ["RCON"] = ["Remote Console", "Remote Control"],
        ["IW"] = ["Infinity Ward"],
        ["COD"] = ["Call of Duty"],
        ["MW"] = ["Modern Warfare"],
        ["BO"] = ["Black Ops"],
        ["WZ"] = ["Warzone"],
        ["BR"] = ["Battle Royale"]
    };

    /// <summary>
    /// Word list for Fill In The Blank game. Gaming/CoD themed words.
    /// </summary>
    public List<string> FillInBlankWords { get; set; } =
    [
        // Weapons
        "SNIPER", "SHOTGUN", "PISTOL", "RIFLE", "GRENADE", "KNIFE", "ROCKET",
        "LAUNCHER", "CARBINE", "REVOLVER", "CROSSBOW", "EXPLOSIVE",
        
        // Actions
        "RELOAD", "SPRINT", "CROUCH", "PRONE", "JUMP", "SLIDE", "CLIMB",
        "THROW", "PLANT", "DEFUSE", "CAPTURE", "DEFEND", "ATTACK",
        
        // Game terms
        "KILLSTREAK", "HEADSHOT", "CAMPING", "RUSHING", "FLANKING", "SNIPING",
        "QUICKSCOPE", "NOSCOPE", "HIPFIRE", "WALLBANG", "COLLATERAL",
        
        // Equipment
        "CLAYMORE", "TROPHY", "FLASHBANG", "SMOKE", "STUN", "TACTICAL",
        "LETHAL", "EQUIPMENT", "PERK", "ATTACHMENT", "OPTIC", "SUPPRESSOR",
        
        // Roles/Players
        "SOLDIER", "SNIPER", "MEDIC", "CAPTAIN", "COMMANDER", "OPERATOR",
        "PLAYER", "ENEMY", "TEAMMATE", "SQUAD", "PLATOON",
        
        // Maps/Locations  
        "BUNKER", "TOWER", "BRIDGE", "HARBOR", "FACTORY", "WAREHOUSE",
        "AIRPORT", "STADIUM", "HOSPITAL", "COMPOUND", "OUTPOST",
        
        // Misc gaming
        "VICTORY", "DEFEAT", "SPAWN", "RESPAWN", "LOBBY", "SERVER",
        "PRESTIGE", "UNLOCK", "CAMO", "EMBLEM", "CALLING", "LOADOUT"
    ];
}
