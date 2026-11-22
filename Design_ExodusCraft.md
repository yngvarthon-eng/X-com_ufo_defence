# Exodus Craft & the Lier Basin

Design notes for a family of human-built craft and the "new exodus" storyline.
Visual concept sheet (diagrams, flight modes, Lier map, journey):
https://claude.ai/artifact/4sPpqTfxtkyuRqJpsxQpAj

Ties into existing GDD sections:
- **17. Human Usage of UFOs & Criminal Enterprises:** exodus craft are human-built ("earthly tech"), not captured alien craft.
- **Smuggler UFO** (`Assets/Prefabs/UFO/SmugglerUFO.prefab`): the same airframe family could be used by smugglers.
- **15. Human Factions:** the Exodus movement as a new human faction.
- **13. Dark Aliens:** open question: are they connected to the exodus, or opposing it?

---

## 1. Core idea: two propulsion systems

| System | Used for | How it works | Weakness |
|---|---|---|---|
| **Resonance brake ring** | Descent braking, hover, slow sink, steering near ground | Ring of speaker-like chambers under the hull. A magnet and coil vibrate a membrane, and a nozzle turns the vibration into downward vortex-ring jets ("synthetic jets"). | Weak per watt, loud hum, needs air |
| **Rim fans** | Climb, cruise in lower atmosphere | Ducted fans built into the hull rim | Useless in thin air (above ~20 km) |
| **Mothership tether** | Final climb / docking | Mothership waits high in the stratosphere and winches the lander up | Mothership must be in position |

Flight modes (ring power per cell): Brake descent 100 %, Climb 90 %, Hover 65 %, Slow sink 45 %,
Fly forward = rear cells stronger than front cells (craft tilts nose-down and moves forward).

Cell layouts:
- **Variant A, 24 small cells:** smooth control, higher pitch, survives losing a cell.
- **Variant B, 6 big cells:** deep loud hum, more push per cell, coarse steering.

### Game hooks
- **Detection:** the hum is a signature. Acoustic/seismic sensors detect landers while they are braking or hovering, but not while they climb on fans.
- **Damage model:** a damaged brake ring means a hard landing, which creates a crash-site mission instead of a clean escape.
- **Weather:** strong wind degrades hover and steering. Thin air or high altitude disables the fans.
- **Blackouts (GDD 20):** the ring needs heavy power, so a power blackout grounds landers.

---

## 2. Craft generations

| Gen | Name (working) | Size | Propulsion | Role | Status |
|---|---|---|---|---|---|
| 0a | Pilgrim lander (low-tech, crewed) | large | Brake ring, very weak lift | Carries people, mostly downward | described |
| 0b | Mule (low-tech, cargo only) | medium | Brake ring + fans | Smuggling, often hazardous goods | described |
| 1 | Resonance Ring Lander | tens of metres | Brake ring + rim fans | Ground ↔ mothership shuttle | concept done |
| 2 | Lier-class mothership | 1.15–1.6 km | ? (has under-dome, brake ring) | Carrier / exodus ship | concept done |
| 3 | Sun-diver mothership | very large | ? | Works close to the Sun, mines Mercury, hides in the Sun's glare | described |

### Gen 0a: Pilgrim lander (low-tech, crewed)
- Rather big, built to carry many people.
- **Poor upward propulsion:** it comes down well on its brake ring but climbs slowly and with difficulty. Some trips may be one-way.
- **Seating:** passengers sit back to back around small columns, 4 per column, facing outward.
- **Passengers' belief:** they think they arrive by divine intervention.
- **Origin lore:** the craft (or its makers) come from **before the last ice age** (more than ~12,000 years ago).
- **Elder species:** passengers tell of elder species who *also came from Earth*: **the Dark** and **the Troll**.
  - Hook: the GDD's Dark Aliens may not be alien at all, but an elder Earth species. Trolls tie the story to Norwegian folklore and mountains.

Game hooks:
- Slow climb, so Pilgrim landers are easy to intercept after pickup. The moral weight lies in deciding whether you should.
- Passengers who were recovered can be interviewed, unlocking lore about the elder species (Intelligence / research).
- Column seating means hits to the hull core are costly. A rescue mission should avoid heavy weapons.

### Gen 0b: Mule (low-tech, cargo only)
- Carries **smuggler goods only**. There is no life support and it is not suitable for humans.
- Cargo ranges from harmless to very dangerous: heavy metals, drug precursors (e.g. pseudoephedrine), up to **nuclear warheads**.
- Natural fit for the existing **Smuggler UFO** prefab and GDD section 17.

Game hooks:
- **Cargo is unknown until scanned or recovered.** Shooting one down over a city could spread toxic or radioactive material.
- Crash sites get a hazard level (none / toxic / radioactive) that changes the squad loadout and mission rules.
- Cargo leads to the criminal network: who sent it, who receives it (NIA, Human Factions).
- Seizing a warhead is a major story event with political fallout.

### Lier-class mothership
- Lens hull with a large **dome on the underside** and a small dome on top.
- A ring of brake cells between the rim and the under-dome.
- The under-dome takes the impact in a hard landing and sinks into soft ground, so the hull stays intact.

---

## 3. The Lier Basin (map / mission site)

From a dream: three large motherships lie in the basin of Lierdalen, landed hard but completely intact,
filling the valley so a fourth would not fit.

- Valley floor width: roughly 2–3 km (estimate, check on norgeskart.no). Length ~20 km, Drammensfjorden to Sylling.
- Arrangement A, **in a line** along the valley: each ship ≈ 1.6 km across.
- Arrangement B, **two side by side, one further north**: each ship ≈ 1.15 km across.
- Soil pushed up in an arc on the north side of each ship where it slid.

### Mission ideas
- **Lier Basin (recovery):** three intact hulls on Norwegian soil. Secure the site before criminal groups or rival factions strip the tech. NIA (GDD 21) knew they were coming.
- **Exodus Night (moral choice):** landers are picking up civilians. Intercept them (defence), escort them (exodus), or investigate whether it is really an exodus or an abduction.
- **Hard Landing:** a lander's brake ring fails during descent. Rescue the passengers and recover the ring cells for research.

---

## 4. The exodus journey (6 steps)

1. **Gathering:** travellers wait on the valley floor. A lander comes down.
2. **Braked descent:** the ring runs at full power and slows the fall to walking speed.
3. **Boarding:** legs and ramp deploy, and people board under the dome.
4. **Climb:** the ring goes off, the rim fans go on, and the lander lifts above the clouds.
5. **Handover:** around 20 km up the air is too thin, so the mothership tether winches the lander up.
6. **Docking:** the lander enters the bay and the travellers transfer to the mothership.

Each step can be a phase in an interception mission: the player's window to act shrinks as the lander climbs.

---

## 5. Deep lore: the hidden builders and the three heavens

### Origin of the craft
- The designs are **human, from before the last ice age**. A lost civilisation built them.
- The craft were **stored and serviced on a hidden Moon base**, unknown to humankind on Earth.
  - Plausible setting detail: real lava tubes and pits exist on the Moon (e.g. the Marius Hills pit), natural caverns for an underground base.
- **Mars** as an option: an older or deeper site, too far for routine servicing, maybe where it all started.

### The three heavens
- Norse sources name three heavens: Snorri's *Gylfaginning* says that above our sky lies **Andlang**, above it **Víðbláin**, and that **Gimlé** stands at the southern end of heaven and will survive Ragnarök.
- One of the heavens is **Atlantis**.
- **Canon: the heavens are motherships.** They are extremely large and capable of long journeys. The Moon and Mars are just *bases*.

| Heaven (mothership) | Myth name | Notes |
|---|---|---|
| 1 | Atlantis | The heaven-ship remembered on Earth as a lost land |
| 2 | Andlang | Above our sky |
| 3 | Víðbláin | Highest and farthest |

- **Bases:** the Moon (storage and service, in lava tubes) and Mars (older site).
- **The heavens carry no small craft** (to save weight). Everyone who wants to reach them must come up in ships built on Earth. That is why the low-tech landers exist.
- **The fall of the heavens:** a newer power came and took the three heavens from the old order (Odin's).
  This power is **Dr X and Dr Y**: invented figures, not tied to any real religion.
  - They try not to work against each other. Part of the world belongs to Dr X, part to Dr Y.
  - Conflict between them only drags out their task, which makes things harder for them too.
  - **Dr X controls the Dream Machine.** Dr Y refuses it and openly opposes the people who run it.
  - See section 8, "Dr Y", for who Dr Y is.

### The Dark (Trolls / Demons): one species
"Troll" and "demon" are human names for the same elder species of Earth: **the Dark**.
(This replaces the GDD's separate "Dark Aliens" idea: they are not aliens, they are from Earth.)

- **Fear light** and show themselves only at night (in Norwegian folklore trolls turn to stone in sunlight).
- **Fear men even more than light**, though only some men (a soldier trait, see below).
- **The three motherships in Lier were their landing.** Only one or two are still capable of flight, or almost.
- **Shapeshifters:** they can take a human form and change back again.
- **Two sets of eyes:** alien eyes and human eyes. When the disguise slips, one set slides up or down over the other. This is the tell.
- **Kinship view:** they understand difference between beings only as **father and son**. Every relationship is a generation above or below, never "other".
- **They rarely see us awake.** They come to humans while they sleep, which connects them to the Norse *mara* (the nightmare spirit that sits on sleepers).
- **Light-blind:** bright light blinds them rather than hurts them. This is why they fear it. In light they are helpless and must rely on other senses (hearing, smell, vibration).

Extra game hooks:
- **Flash = blind:** flares and floodlights blind Dark units for some turns or seconds. Blinded units attack whatever makes noise.
- **Sleeping base:** Dark raids happen when base staff sleep. Night shifts and guards awake reduce the risk.
- **Awake witnesses:** a soldier who sees a Dark while awake is rare. It could give a special event, a trauma risk, or a lore unlock.

#### Variants
Slightly different from each other, some with more human-looking eyes.

| Variant | Height | Look | Notes |
|---|---|---|---|
| **Common Dark** ("son") | A little shorter than a man, almost human stature | Dark, near-human shape | Most common. Best at passing as human. |
| **Half-turned** | Human height | More human eyes | Spends most of its time in human form. Infiltrator. |
| **Ill Dark** ("father") | Bigger than a man | Larger, sickly, twisted | Rarer and more dangerous. Cannot fully pass as human. |

Game hooks:
- **Night-only enemy:** activity happens between dusk and dawn, so the day/night cycle on the world map matters.
- **Light as defence:** floodlights, flares and UV weapons as research items. Bases with strong lighting are safer.
- **Soldier trait: feared.** Some soldiers carry a trait that makes the Dark afraid of them. Nearby Dark lose morale, may flee, or refuse to attack. Rare, can't be trained, and the reason is a story secret.
- **Infiltration (links to GDD 16 spy mechanics):** a shapeshifted Dark can be hidden among staff, civilians or even recruits.
  - **The eye tell:** in the Info-Box or a close-up, an alert soldier may notice the eyes slip. Better detection research or perception traits raise the chance.
  - Accusing the wrong person costs morale and trust, which fits "Never Sack the Spy".
- **Father/son AI:** Dark units follow their "father" (the nearest Ill Dark). Killing the father makes the sons scatter or go wild.
- **Lier Basin, revisited:** a race to reach the last flight-capable mothership. Destroy it, repair it, or let the Trolls leave?
- **Repair clock:** each night the Trolls work on the ships, and each day they hide. The player has days to decide.

## 6. Story arc: The Mutation

### Premise
- **1990s:** a foreign state runs a secret **DNA experiment and hypnosis program** against Norway.
  (Working name: Russia. For release, consider an invented state or agency, like Dr X / Dr Y, so the story doesn't depend on a real country.)
- The program makes Norwegians **mutate**. Two strains appear at about the same time:
  1. **Troll strain:** carriers can shapeshift between human and Dark form, with the two sets of eyes.
  2. **Reptile strain:** a reptile organism grows inside the host, slowly takes over body and mind, and at the end of the transformation devours and replaces the human completely.
- The mutated thin out the population for many reasons and by many means.

### The protagonist: "the one they fear"
- A huge, calm man. The mutated are furious with him but **afraid of something in him**, and they do not kill him.
- He never attacks first. He stays calm as long as possible.
- The **eye thing**: some of them started it. Meeting their eyes, the disguise slips.
- His quiet weapon: **"I know. I know."** Some of them break simply because he knows.
- Links to the **Feared** soldier trait (section 5).
- He is never alone for long, because he always has a companion. Alone, he is weaker. Together, they hold.

### Gameplay ideas
- **Companion system:** the protagonist has a constant companion. Being separated raises risk and lowers morale.
- **Calm meter:** staying calm de-escalates encounters. Looking angry makes aggressive humans and some mutated back down. Attacking first ends the advantage.
- **Response timer:** in civilian scenes, help arrives in 15–45 minutes. Survive until then by keeping distance, using cover and light, and talking them down.
- **"I know" interrogation:** in rare scenes the player confronts a revealed mutant. Success makes them confess or flee.
- **Hospital chapter:** the protagonist starts in a closed ward. The first Dark sighting happens in the courtyard at night: a woman's voice ("Is he here?"), the troll turns, is terrified, and is told to stand side-on.

## 7. The Dream Machine and the factions (draft)

### The Dream Machine
- A mind-influence technology, started officially in the 1950s or earlier, possibly a gift from a "grey" alien to a Cold War state.
- It **only makes dreams**: it plants images, hints and "knowledge of the future" in sleepers to steer them. It is not full control.
- An older, cruder weapon came first: **voices and hypnosis**, imposed madness as a weapon. The state that made it found it "not useful on its own territory" and turned it outward.
- Later both sides got the technology. In the end it is even used by police forces for crowd control. This is the world's greatest terror, and the last terror before the exodus.
- **Real history to draw on** (for flavour and research):
  - CIA **MKUltra** (1953–1973): real mind-control experiments with drugs and hypnosis.
  - **Soviet punitive psychiatry**: dissidents were diagnosed with "sluggish schizophrenia" and locked in psychiatric hospitals.
  - Cold War "psychotronics" research on both sides. It never worked, but it was funded.

Game hooks:
- **Dream events:** soldiers' dreams give hints about upcoming missions. Some are true, some are planted by the enemy.
- **Dream Machine sites** as late-game targets. Shutting one down lifts "influence" from a region.
- **Influence level per region:** high influence means panic, unrest and recruits that may be compromised.

### The exodus, placed in the timeline
Comes **after the smoke clears** from the mind-control era. Driven by fear and religion: faith loses its grip on the world, or something frightens everyone.

### Factions (working names; invented names recommended for release)
Organised by **what they do**, not by ethnicity, so the villains are programs and ideologies rather than whole peoples.

| Working idea | Invented name (proposal) | Role |
|---|---|---|
| Russian state program | **The Directorate** | Dream Machine, voices, DNA program. Main human villain. |
| American program | **Bureau 53** | MKUltra-style rival. Ally or enemy depending on choices. |
| European neo-Nazi movement | **The Pale Order** | Militia that preys on the innocent and wants the mutation tech. |
| Middle-East power bloc | **The Southern Compact** | Regional power with its own agenda, trading tech and oil. Neutral, not evil by default. |
| Innocent population | **The Unturned** | Civilians to protect, and future exodus travellers. |
| Alien 1 | **The Greys** | Gave the Dream Machine to humans. Watchers. |
| Alien 2 / elder | **The Dark** | Trolls and demons, Earth's elder species (see section 5). |
| Alien 3 | **The Reptile strain** | Organism that grows inside hosts and replaces them. |
| Above all | **Dr X and Dr Y** | The newer power that took the three heavens. |

### World regions (status at game start, proposal)
- **Scandinavia:** ground zero, mutation outbreak.
- **Russia / Eastern Europe:** source of the Directorate.
- **Asia:** unknown, quiet. Calm, or under a different influence? A mystery to investigate.
- **India:** firm in faith, resistant to the Dream Machine. Sacred sound (Nada) blocks it. This ties back to the temple and the vibration ring.
- **Americas:** Bureau 53 territory.

## 8. Canon decisions

### Exodus or abduction: the player's choice
- The game never answers it for the player. The player's actions decide which one it becomes.
- **Win/loss condition:** e.g. an *Exodus ending* (the player protects and launches the ships with willing travellers) versus an *Abduction ending* (ships leave with taken people, and the player failed or chose to look away). A third ending, *Grounded*, could come from stopping every launch.

### Who built the craft
- **The Dark built first.** The early low-tech craft were a demand from God: the Dark had to build them.
- **Humans copied the idea** and started building their own, slightly different versions of low-tech craft.
- Gameplay: Dark-built and human-built Gen 0 craft differ (crash sites show different materials and layouts, and research gives different results).

### The Dark and God
- The Dark believe they work according to God's will, through mental and physical projects humans can't see yet.
- **They are God's first attempt at making humanoids.** After a while God judged them unworthy, and humans came after.
- **On family:** they believe man and wife is the human duty, and they are content when humans understand that. Among themselves they know only father and son, because **their wives left them**, and they may not know where the wives went.
  - Story hook: *Where did the wives of the Dark go?* A late-game mystery.
- Toward the exodus: they see it as part of God's plan, which they serve in their own way. They are neither simple allies nor simple enemies.

### The Mule network
- Run by a **neo-Nazi network inside government positions**.
- They need the change too, to save themselves, but believe any means against their enemies is allowed.
- Their method: brutalise people so the victims carry lifelong rage, then use that rage to push the population to the far right.
- **Content note for release:** keep their atrocities implied and off-screen. Sexual abuse of children cannot be depicted in a commercial game: age-rating boards and stores (Steam, consoles) refuse it. Showing that the Pale Order is monstrous through consequences is enough.

### Gen 3: Sun-diver mothership
- Can move close to the Sun, work and mine on **Mercury** with ease, and **hide in the Sun's glare**.
  - Real basis: objects near the Sun in the sky are very hard to see with telescopes, so "hiding in the Sun" works as a stealth mechanic.
- **The Dark thrive at a stable 80 °C** ("hot-ass hell").
  - Real basis: Mercury's day side reaches about 430 °C and its night side about −180 °C. Along the line between day and night, and in some crater rims near the poles, there are zones in between, where an 80 °C Dark colony could live.
- This is meant to be the peaceful, calm era, the time after the exodus.
- **It must not land on Earth.** It would fill a whole valley basin (e.g. Åmot) and scorch everything under and around it.
- When near Earth it **rests on the tops of old volcanoes**, placed strategically.
  - Real basis: the Oslo Rift has remains of volcanoes about 300–250 million years old (lava plateaus such as Krokskogen), close to Lier, Sylling and Modum. These could be mission sites.
- Propulsion is still open.

### Where the exodus goes
- To the **three heaven motherships** (Atlantis, Andlang, Víðbláin).
- The heavens bring no rescue ships, so humankind must build a great number of landers itself. This drives the base-building and production side of the game.

### The "feared" trait: Elder Blood (vampire lineage)
- Some humans carry an old bloodline: **vampires**. There are several kinds.
  - **Elf type:** four fangs (two upper, two lower) that grow out during "phenomenon times". This is the protagonist's lineage, inherited from his mother.
  - **Twelve-fang type:** a rarer, different kind.
- **Why the Dark fear them:** they do not petrify easily and can look a troll straight in the eyes without being caught by it.
- Through history, vampires have helped humans with troublesome trolls.
- Gameplay: soldiers with Elder Blood are immune to the Dark's gaze and fear effects, and break Dark morale. Fangs appear only during certain events (phenomena), which could be a temporary power mode.
- **Phenomenon times** are planetary: the movements of the planets and weather events.
  - Elder Blood can **hear the Sun hum and buzz** when it comes too close. (Real basis: the Sun really vibrates with pressure waves; NASA has turned them into audible sound.)
  - **Thunder sends them into deep sleep**, for the thunder god's sake. If they smell or feel him, they may change a little in looks and stature.
- Gameplay:
  - **Planetary calendar** on the world map. Alignments and solar events trigger Elder Blood power mode.
  - **Thunderstorms** put Elder Blood soldiers to sleep: a real weakness. Enemies who know this attack during storms. This links to the GDD's weather systems.

### Inspiration
Deeply inspired by religion, many of them, not just one. Stories grow from collecting what can be seen and heard about all of them:
Indian sacred sound (Nada Brahma, Shiva's drum), Ezekiel's "wheels within wheels", Norse cosmology (Odin, the three heavens, Gimlé), Norwegian troll folklore, the *mara*.

### Dr Y
- Has a **divine duty**, given by God long ago. The people have relieved him of it.
- **Bloodline:** descended from a **beheaded king of Germany**. (Possible historical inspiration: Conradin, the last Hohenstaufen, claimant to the German crown, beheaded in Naples in 1268.)
  - The king's later relatives share a "schizophrenic genome". It is really an inheritance that resembles modern human technology, carried since God gave them the duty.
- **Alone.** He sides with both God and the Dark. No world leader can match him, though they try.
- **Karma:** he experiences extreme good karma on different sides of the story, but karma can turn mean too.
- **Cold and fearless.** He knows enemies will appear at random. He hates killing them, so he saves their lives instead and appeals to their humanity.
- **Refuses the Dream Machine** and openly opposes those who control it. So **Dr X is the controller**.
- **Dr Y is the protagonist.** "The one they fear" turns out to be Dr Y, which could be the game's big late reveal.

Gameplay ideas:
- **Mercy system:** defeated enemies can be spared and appealed to. Spared enemies may return as allies, informants, or (bad karma) as worse enemies.
- **Karma meter** instead of a simple good/evil scale: it swings in surprising directions.
- **Random encounters:** enemies appear without warning. Dr Y stays calm and chooses mercy or force.

## 9. Still open
- Where did the wives of the Dark go? (Still thinking.)
- How does Gen 3 move?
- Who is Dr X? What does Dr X want with the Dream Machine?
