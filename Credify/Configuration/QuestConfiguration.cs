using Credify.Chat.Passive.Quests.Enums;
using Credify.Chat.Passive.Quests.Models;

namespace Credify.Configuration;

public class QuestConfiguration
{
    public Dictionary<MeansOfDeath, List<string>> MeansOfDeathLookupTable { get; set; } = new()
    {
        [MeansOfDeath.Suicide] = ["MOD_FALLING", "MOD_SUICIDE"],
        [MeansOfDeath.Headshot] = ["MOD_HEAD_SHOT"],
        [MeansOfDeath.Pistol] = ["MOD_PISTOL_BULLET"],
        [MeansOfDeath.Rifle] = ["MOD_RIFLE_BULLET"],
        [MeansOfDeath.Impact] = ["MOD_IMPACT"],
        [MeansOfDeath.Explosive] =
        [
            "MOD_EXPLOSIVE",
            "MOD_EXPLOSIVE_BULLET",
            "MOD_PROJECTILE",
            "MOD_PROJECTILE_SPLASH",
            "MOD_GRENADE",
            "MOD_GRENADE_SPLASH"
        ],
        [MeansOfDeath.Melee] =
        [
            "MOD_MELEE",
            "MOD_MELEE_ALT",
            "MOD_MELEE_WEAPON_BUTT"
        ],
    };

    public Dictionary<Weapon, List<string>> WeaponLookupTable { get; set; } = new()
    {
        [Weapon.RiotShield] = ["riotshield_mp"],
        [Weapon.Grenade] = ["frag_grenade_mp"],
        [Weapon.Silenced] = ["_silencer_"]
    };

    public List<Quest> Quests { get; set; } =
    [
        new Quest
        {
            Enabled = true,
            Name = "London Simulator (Melee Kills)",
            Reward = 1_000,
            ObjectiveType = ObjectiveType.Melee,
            ObjectiveCount = 10,
            IsRepeatable = false,
            IsPermanent = false,
        },
        new Quest
        {
            Enabled = true,
            Name = "Yap Champion (Chats)",
            Reward = 1_000,
            ObjectiveType = ObjectiveType.Chat,
            ObjectiveCount = 25,
            IsRepeatable = false,
            IsPermanent = false,
        },
        new Quest
        {
            Enabled = true,
            Name = "The Wall (Shield Kills)",
            Reward = 2_000,
            ObjectiveType = ObjectiveType.RiotShield,
            ObjectiveCount = 20,
            IsRepeatable = false,
            IsPermanent = false,
        },
        new Quest
        {
            Enabled = true,
            Name = "Head Hunter (Headshots)",
            Reward = 1_000,
            ObjectiveType = ObjectiveType.Headshot,
            ObjectiveCount = 20,
            IsRepeatable = false,
            IsPermanent = false,
        },
        new Quest
        {
            Enabled = true,
            Name = "Reverse Psychology (Suicides)",
            Reward = 500,
            ObjectiveType = ObjectiveType.Suicide,
            ObjectiveCount = 5,
            IsRepeatable = false,
            IsPermanent = false,
        },
        new Quest
        {
            Enabled = true,
            Name = "Team Player (Kills)",
            Reward = 100_000,
            ObjectiveType = ObjectiveType.Kill,
            ObjectiveCount = 100_000,
            IsRepeatable = false,
            IsPermanent = true,
        },
        new Quest
        {
            Enabled = true,
            Name = "Big Spender (Spend Credits)",
            Reward = 100_000,
            ObjectiveType = ObjectiveType.CreditsSpent,
            ObjectiveCount = 1_000_000,
            IsRepeatable = false,
            IsPermanent = true
        },
        new Quest
        {
            Enabled = true,
            Name = "Raffle King (Play Raffles)",
            Reward = 25_000,
            ObjectiveType = ObjectiveType.Raffle,
            ObjectiveCount = 4,
            IsRepeatable = false,
            IsPermanent = true
        },
        new Quest
        {
            Enabled = true,
            Name = "Blackjack Boss (Play Blackjack)",
            Reward = 10_000,
            ObjectiveType = ObjectiveType.Blackjack,
            ObjectiveCount = 25,
            IsRepeatable = false,
            IsPermanent = true
        },
        new Quest
        {
            Enabled = true,
            Name = "Roulette Renegade (Play Roulette)",
            Reward = 10_000,
            ObjectiveType = ObjectiveType.Roulette,
            ObjectiveCount = 25,
            IsRepeatable = false,
            IsPermanent = true
        },
        new Quest
        {
            Enabled = true,
            Name = "Generous Gambler (Donate $500)",
            Reward = 1_000,
            ObjectiveType = ObjectiveType.Donation,
            ObjectiveCount = 1,
            IsRepeatable = false,
            IsPermanent = false
        },
        new Quest
        {
            Enabled = true,
            Name = "High Roller (Win $10k)",
            Reward = 10_000,
            ObjectiveType = ObjectiveType.Baller,
            ObjectiveCount = 1,
            IsRepeatable = false,
            IsPermanent = false
        },
        new Quest
        {
            Enabled = true,
            Name = "Top G (Reach Top 5)",
            Reward = 250_000,
            ObjectiveType = ObjectiveType.TopHolder,
            ObjectiveCount = 1,
            IsRepeatable = false,
            IsPermanent = true
        },
        new Quest
        {
            Enabled = true,
            Name = "Hot Potato (Grenade Kills)",
            Reward = 10_000,
            ObjectiveType = ObjectiveType.HotPotato,
            ObjectiveCount = 25,
            IsRepeatable = false,
            IsPermanent = false
        },
        new Quest
        {
            Enabled = true,
            Name = "No Impact (Impact Kills)",
            Reward = 10_000,
            ObjectiveType = ObjectiveType.Impact,
            ObjectiveCount = 2,
            IsRepeatable = false,
            IsPermanent = false
        },
        new Quest
        {
            Enabled = true,
            Name = "Silent Assassin (Silenced Kills)",
            Reward = 1_500,
            ObjectiveType = ObjectiveType.Silenced,
            ObjectiveCount = 50,
            IsRepeatable = false,
            IsPermanent = false
        },
        new Quest
        {
            Enabled = true,
            Name = "Geoffrey (Say 'My Name Jeff')",
            Reward = 2_000,
            ObjectiveType = ObjectiveType.MyNameJeff,
            ObjectiveCount = 3,
            IsRepeatable = false,
            IsPermanent = false
        },
        new Quest
        {
            Enabled = true,
            Name = "Humiliation (Get knifed)",
            Reward = 2_000,
            ObjectiveType = ObjectiveType.Humiliation,
            ObjectiveCount = 10,
            IsRepeatable = false,
            IsPermanent = false
        },
        new Quest
        {
            Enabled = true,
            Name = "Trivia Fanatic (Answer Chat Trivia)",
            Reward = 250_000,
            ObjectiveType = ObjectiveType.Trivia,
            ObjectiveCount = 50,
            IsRepeatable = false,
            IsPermanent = true
        }
    ];
}
