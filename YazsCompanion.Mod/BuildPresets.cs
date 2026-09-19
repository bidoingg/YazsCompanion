// The preset builds per survivor. Each follows one of the survivor's two tier-2 weapon branches (they deal
// different damage types, so everything behind them lines up differently) or an identity the kit allows.
// What a preset fixes is what defines it; what it leaves open (an empty branch, an ability without an evolution
// entry) is decided live from the squad - the evolution sharing a damage type with the squad's stacked tag, or
// carrying a tag that a team passive on the squad boosts.
//
// Where they come from (2026-09 review; sources in Knowledge.cs):
//   Guides = a build human guide writers and forum regulars describe (Kudesnik's 0.7 guide, the Steam forum threads
//            on go-to builds / best weapon per class / One Hit / Endurance, Destructoid's 1.0 tier list), checked
//            against the 1.0 patch notes. The first preset of every survivor is the guides' main build.
//   Data   = an alternative read from the game's own data (damage types, powerup tags, the team passives Grenade /
//            Turret / Trap Expertise and Cold Chain) where the guides are silent. Honest about it: Medic and Mechanic
//            have ONE build in the guides; their alternatives say so.
// Evolution picks are only fixed where a source names one or the build's identity depends on it.
using System.Collections.Generic;

namespace YazsCompanion
{
    internal static class BuildPresets
    {
        const string Guides = "guides", Data = "data";

        static Build B(string id, string survivor, string name, string source, string glyph, BuildStyle style, string branch, string summary, string[] abilities, string[] wants, params string[] evolutions)
        {
            var b = new Build { Id = id, Survivor = survivor, Name = name, Source = source, Glyph = glyph, Style = style, Branch = branch ?? "", Summary = summary };
            b.Abilities.AddRange(abilities); b.Wants.AddRange(wants);
            for (int i = 0; i + 1 < evolutions.Length; i += 2) b.Evolution[evolutions[i]] = evolutions[i + 1];
            return b;
        }

        public static List<Build> All()
        {
            return new List<Build>
            {
                // ---------------------------------------------------------------- SWAT
                B("swat-rifleman", "SWAT", "Rifleman", Guides, "bullets", BuildStyle.Weapon, "Assault Rifle",
                    "The consensus build: finish the Assault Rifle, then the helicopter, then the turret. All kinetic, one tag. Grenade Trail last.",
                    new[] { "Helicopter Strike", "Automatic Turret", "Ricochet", "Grenade Trail" }, new[] { "weapons", "Kinetic", "critical" }),
                B("swat-operator", "SWAT", "Operator", Guides, "turret", BuildStyle.Ability, "Sniper Rifle",
                    "Ability first (Kudesnik's guide): the helicopter once, max the turret, then Ricochet - it scales with ABILITY damage and crit. Sniper Rifle last; also the Boss Rush gun.",
                    new[] { "Automatic Turret", "Ricochet", "Helicopter Strike", "Grenade Trail" }, new[] { "abilities", "critical", "turret", "deployable" }),
                B("swat-grenadier", "SWAT", "Grenadier", Data, "blast", BuildStyle.Balanced, "",
                    "Explosives on the way to the Grenade Launcher. Grenade Expertise boosts every grenade on the squad, so take the evolutions that throw them. Chemtrails also feeds a chemical squad.",
                    new[] { "Grenade Trail", "Helicopter Strike", "Automatic Turret", "Ricochet" }, new[] { "Explosive", "abilities", "grenade" },
                    "Helicopter Strike", "Helicopter Strike: Chemtrails", "Automatic Turret", "Automatic Turret: Demolition"),

                // ---------------------------------------------------------------- Tank
                B("tank-demolition", "Tank", "Demolition", Guides, "blast", BuildStyle.Weapon, "Rocket Launcher",
                    "Rocket Launcher (viable at any range since 0.9.2) with Bombing Strike first: one explosive tag from the weapon and two abilities. The saw and the mines are late insurance.",
                    new[] { "Bombing Strike", "Minefield", "Sawblade Drone", "Fury Unleashed" }, new[] { "Explosive", "abilities" }),
                B("tank-reaper", "Tank", "Saw Reaper", Guides, "gear", BuildStyle.Ability, "",
                    "Ability first (Kudesnik's guide): max Sawblade Drone into Cogwheels, then the shotgun, then Minefield: Taunt for crowd control. A walking reaper next to Ghost's slashing kit.",
                    new[] { "Sawblade Drone", "Minefield", "Fury Unleashed", "Bombing Strike" }, new[] { "Slashing", "melee", "taunt", "abilities", "armor" },
                    "Sawblade Drone", "Sawblade Drone: Cogwheels", "Minefield", "Minefield: Taunt"),
                B("tank-shotgunner", "Tank", "Shotgunner", Guides, "bullets", BuildStyle.Weapon, "",
                    "The shotgun line to the Super Shotgun (forum: the top damage in hour-long runs). Close range: shotguns fall off sharply with distance. Fury Unleashed keeps you in the fight.",
                    new[] { "Fury Unleashed", "Sawblade Drone", "Bombing Strike", "Minefield" }, new[] { "weapons", "Kinetic", "armor", "healing" }),

                // ---------------------------------------------------------------- Engineer
                B("engineer-anchor", "Engineer", "Shield Anchor", Guides, "shield", BuildStyle.Ability, "",
                    "The broad consensus: max the Energy Shield first. It carries One Hit, Endurance and every squad that stands its ground; the turret second.",
                    new[] { "Energy Shield", "Electric Turret", "Electrocution", "EMP Grenade" }, new[] { "abilities", "Electric", "turret", "deployable" }),
                B("engineer-livewire", "Engineer", "Live Wire", Guides, "bolt", BuildStyle.Ability, "",
                    "Ability damage (Kudesnik's guide): max both Electrocution and the Electric Turret. Note 1.0 cut Electrocution's early ranks - it pays off from level 3.",
                    new[] { "Electrocution", "Electric Turret", "Energy Shield", "EMP Grenade" }, new[] { "Electric", "abilities", "status effects", "turret" }),
                B("engineer-gunner", "Engineer", "Arc Gunner", Data, "crosshair", BuildStyle.Weapon, "Laser",
                    "Weapon carry: Tesla into the Laser (the boss and crit pick; Blaster is the crowd pick) and on to Plasma. Everything electric feeds one tag.",
                    new[] { "Energy Shield", "Electrocution", "Electric Turret", "EMP Grenade" }, new[] { "weapons", "Electric", "critical" }),

                // ---------------------------------------------------------------- Huntress
                B("huntress-bombardier", "Huntress", "Bombardier", Guides, "blast", BuildStyle.Weapon, "Explosive Arrows",
                    "Explosive Arrows with weapon cooldown: the highest recorded Huntress damage. Arrow Rain first (Downpour), Zombie Decoy once.",
                    new[] { "Arrow Rain", "Arrow Penetration", "Zombie Decoy", "Bear Trap" }, new[] { "weapons", "critical", "Explosive", "Kinetic" },
                    "Arrow Rain", "Arrow Rain: Downpour"),
                B("huntress-frost", "Huntress", "Frost Warden", Guides, "snow", BuildStyle.Weapon, "Freezing Arrows",
                    "Freezing Arrows ('take Frost in any case'): the horde never arrives. Arrow Rain first; pairs with Mechanic and a freezing Medic on one ice tag.",
                    new[] { "Arrow Rain", "Bear Trap", "Arrow Penetration", "Zombie Decoy" }, new[] { "Ice", "critical", "slow", "weapons" },
                    "Arrow Rain", "Arrow Rain: Downpour"),
                B("huntress-trapper", "Huntress", "Trapper", Data, "diamond", BuildStyle.Balanced, "",
                    "Taunts and traps under Trap Expertise: Zombie Decoy and Bear Trap hold the horde, the bow finishes it. The guides rate her traps below Arrow Rain.",
                    new[] { "Zombie Decoy", "Bear Trap", "Arrow Rain", "Arrow Penetration" }, new[] { "taunt", "deployable", "abilities" }),

                // ---------------------------------------------------------------- Ghost
                B("ghost-blademaster", "Ghost", "Blademaster", Guides, "blade", BuildStyle.Weapon, "Thousand Cuts",
                    "Near-unanimous: Thousand Cuts up close, one slashing tag from the blade, the shuriken and the kunai. Best behind an Energy Shield; with Holo-bait it is the One Hit build.",
                    new[] { "Pulsar", "Shuriken", "Holo-bait", "Kunai Dance" }, new[] { "Slashing", "melee", "critical", "dodge" }),
                B("ghost-windcutter", "Ghost", "Windcutter", Guides, "up", BuildStyle.Weapon, "Windcutter",
                    "The ranged blade: the largest area and range, the slowest swing, more damage the further it flies. Holo-bait keeps the horde at that distance.",
                    new[] { "Shuriken", "Holo-bait", "Kunai Dance", "Pulsar" }, new[] { "Slashing", "critical", "weapons" }),

                // ---------------------------------------------------------------- Medic (the guides describe one build)
                B("medic-field", "Medic", "Field Medic", Guides, "cross", BuildStyle.Ability, "Freezing Flasks",
                    "The guides' one Medic build: support, abilities first (Experiment 21 before anything), the weapon last. Recruit early. Strongest in runs past 20 minutes.",
                    new[] { "Experiment 21", "Medical Drone", "Stimpack", "Resuscitation" }, new[] { "abilities", "healing", "status effects", "Ice" }),
                B("medic-plague", "Medic", "Plague Doctor", Guides, "flask", BuildStyle.Balanced, "Antidote Flasks",
                    "For chemical squads (Bioweapon, Chemtrails, Toxic Arrows): Antidote Flasks and the chemical half of Experiment 21 on one tag, up to the Syringer.",
                    new[] { "Experiment 21", "Medical Drone", "Stimpack", "Resuscitation" }, new[] { "Chemical", "status effects", "abilities" },
                    "Experiment 21", "Experiment 21: 13", "Medical Drone", "Medical Drone: Offense"),
                B("medic-lifeline", "Medic", "Lifeline", Data, "heart", BuildStyle.Ability, "",
                    "Pure sustain for Hardcore and Endurance: the healing drone, Medicament (at least 100 HP per dose), Resuscitation. Not a guide build - an option the kit allows.",
                    new[] { "Medical Drone", "Stimpack", "Resuscitation", "Experiment 21" }, new[] { "healing", "abilities", "armor" },
                    "Medical Drone", "Medical Drone: Defense", "Stimpack", "Stimpack: Medicament"),

                // ---------------------------------------------------------------- Pyro
                B("pyro-hellblade", "Pyro", "Hellblade", Guides, "blade", BuildStyle.Weapon, "Infernax",
                    "The forum's easy mode: Infernax with Fire Walk and No Pain, No Gain, fully levelled before recruiting. Weapon cooldown, not attack speed. Keep long-range mates away.",
                    new[] { "Fire Walk", "No Pain, No Gain", "Molotov Cocktail", "Makeshift Bomb" }, new[] { "melee", "Fire", "Slashing", "weapons", "armor" }),
                B("pyro-inferno", "Pyro", "Inferno", Guides, "flame", BuildStyle.Weapon, "Flamethrower",
                    "Flamethrower, Fire Walk and Molotov (Spray): everything burns on one fire tag and Backdraft grows with it. Guides: strong early, fades after 25 minutes.",
                    new[] { "Fire Walk", "Molotov Cocktail", "Makeshift Bomb", "No Pain, No Gain" }, new[] { "Fire", "status effects", "abilities" },
                    "Molotov Cocktail", "Molotov Cocktail: Spray"),

                // ---------------------------------------------------------------- Mechanic (the guides describe one build)
                B("mechanic-coldchain", "Mechanic", "Cold Chain", Guides, "snow", BuildStyle.Balanced, "Nitro-Gun",
                    "The guides' one Mechanic build: Nitro-Gun, Ice Turret and Cooling Mods on a single ice tag. They rate the car weak and the Transmitter a trap (you must stand in it).",
                    new[] { "Ice Turret", "Cooling Mods", "Remote Control Car", "Transmitter" }, new[] { "Ice", "slow", "turret", "deployable", "abilities" }),
                B("mechanic-ripper", "Mechanic", "Ripper", Data, "gear", BuildStyle.Weapon, "Chainsaw",
                    "Chainsaw up close with the car as a second blade (Nemesis). The guides rate the Chainsaw poorly - this only earns its place next to Ghost (melee synergy).",
                    new[] { "Remote Control Car", "Cooling Mods", "Ice Turret", "Transmitter" }, new[] { "melee", "Slashing", "weapons" },
                    "Remote Control Car", "Remote Control Car: Nemesis"),

                // ---------------------------------------------------------------- Ranger (thin guide coverage)
                B("ranger-beastmaster", "Ranger", "Beastmaster", Guides, "paw", BuildStyle.Ability, "",
                    "Marks and animals: Good Boy (Dobermann for bosses) and the Falcon (Hunting Sweep for damage) hunt what the crossbow marks. Guide coverage of Ranger is thin.",
                    new[] { "Good Boy", "Falcon", "Animal Whistle", "Incense" }, new[] { "abilities", "marked", "melee", "critical" },
                    "Good Boy", "Good Boy: Doberman", "Falcon", "Falcon: Hunting Sweep"),
                B("ranger-stormbolt", "Ranger", "Stormbolt", Data, "bolt", BuildStyle.Weapon, "Shockspike",
                    "Shockspike: electric bolts that mark as they chain. Made for Engineer squads - marked enemies are electrified far more often (their synergy node).",
                    new[] { "Falcon", "Animal Whistle", "Good Boy", "Incense" }, new[] { "Electric", "critical", "marked", "weapons" }),
                B("ranger-wirecutter", "Ranger", "Wirecutter", Data, "blade", BuildStyle.Weapon, "Barber",
                    "Barber: barbed wire between two bolts, slashing and marking whole lines. The dog shares its slashing tag; fits Ghost and Saw Reaper squads.",
                    new[] { "Good Boy", "Animal Whistle", "Falcon", "Incense" }, new[] { "Slashing", "marked", "weapons" }),
            };
        }
    }
}
