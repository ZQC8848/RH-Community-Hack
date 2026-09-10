---
status: reference material (2026-08-31; appendix rewritten as an English synopsis 2026-09-10)
source: `Wage Love IFeel (2).pdf` (same folder) - the project's narrative script, not an engineering document
related: "PlayScene Modes and Beat Chart Spec.md", "Dance Place Stages and Standing Spec.md", "Guide Orb Spec.md"
---

# Wage Love / IFEL - Script to Implementation Map

**The script is the narrative design for the whole experience; what this repository builds is a
prototype of a few of its interaction mechanics.**

The cross-reference comes first and the synopsis is an appendix - what an engineer actually needs to
look up is "why is this thing called that, and which part of the script does it correspond to",
rather than the story itself. **The script's own words are in the PDF beside this file**; the
appendix is a beat-by-beat digest with page numbers, not a transcription.

## 1. The names are not arbitrary

A great many asset names in this project look like random strings and are in fact nodes on the
script's timeline. **Anyone who does not know this will read them as placeholder names and rename
them.**

| Asset in the repo | Where it comes from in the script |
|---|---|
| `RH Community Hack` (project name) | **MIT Reality Hack** (p. 9, the Reality Hack footage in the Wage Love campaign video) |
| `Dance_1984Dancinginstreets` | *Dancing in the Street* (1964) → the **1984 Motown 25th Anniversary Special** (p. 3) |
| `Dance_2016compnoholdingback` | **2016 Oculus Launchpad** + the **compassion dance** (pp. 8–9) |
| `Dance_2017Wiacwagelove` | **2017's *When It All Changed*** (WIAC) + **Wage Love** (p. 9) |
| `LaunchPad_Compassion_Dance_test.fbx` | The same - the compassion dance from the Launchpad period |
| **`SuperFusionAncestor`** | The **ancestors** of p. 10 - the DNA test, Lucy, "the volumetric ancestors come down", dancing with ancestors in the slave castles |

> **The dancer model is an "ancestor", not just some humanoid that was to hand.** That robe means
> something specific in the narrative. Be aware of it when swapping the model.

## 2. Implemented mechanics ↔ the script

| Mechanic in the repo | The design in the script |
|---|---|
| **Guide orbs, `GuideOrb`** | The **dance guide** on p. 3: "a translucent, spirit looking character that is ageless, genderless, almost like the smoke from a flame turned to life", showing you moves to follow |
| **`DanceFollowScore` follow rate** | The **"follow the groove" bar** of pp. 4 / 6: "maybe there's a bar that grows as you move" |
| **`BeatComboTrail` levels** | The **XP points and Totem rewards** of pp. 3–4 ("FEELING THE GROOVE", "DANCING THROUGH TIME", a Hip Hop Totem) |
| **Beat mode** | p. 8: "here we get a type of **beat saber dance teacher** who teaches us moves one by one that will be put together in the next dance" |
| **The Dance Places + travel between them** | The **mini worlds** minigame of p. 6; and the opening game-mechanic note about teleporting to different areas |
| **The green-screen chroma-key shader** | The **Viggle videos** throughout - "we mix 2D Viggle videos with 3D characters and NPC's" (p. 4), "Viggle videos of people doing the Wage Love dance from all over the world" (p. 9) |

> **Chroma keying matters more than it looks.** It is not an art option for making the screen look
> nicer; it is this project's core means of putting **real people from around the world** into a 3D
> space, and the script leans on it from beginning to end. So the upgrade path in
> [Dance Place spec §6.2](Dance%20Place%20Stages%20and%20Standing%20Spec.md) - "if the edges are not
> good enough, key before compression and store with alpha" - is not an optional extra.

## 3. The script answers two open design questions

Both had been parked during implementation; the script answers them plainly.

**3.1 Do the keyed video people replace the 3D dancers? - No, they coexist.**

p. 4: "Perhaps we mix 2D Viggle videos with 3D characters and NPC's." So "green-screen video plus
ancestor models on the same stage" is the design intent, not a stopgap.

**3.2 Should the dancers be aligned to the take? - Yes, and the theme requires it.**

Describing the distant ancestors, p. 3 says: **"their movements all have a semblance of the same
move you're doing."**

The ancestors are dancing **the same dance you are dancing right now** - one groove across time,
which is the central image of the whole piece.

> This changes a recorded conclusion.
> [one-clip-many-dancers.md](../.ai/decisions/one-clip-many-dancers.md) listed "the dancers are not
> aligned to the take" as an **acceptable trade-off**, on the grounds that the clip is 29.93 s and
> the take is 48.67 s. By the script this is **a problem to solve**, not a trade-off: the director
> currently runs on its own clock, and at take time 11.95 s the dancers were measured at 18.34 s.

## 4. The gap: what is built is a mechanic prototype; the script is a complete narrative

Parts of the script with no implementation at all:

- **The narrator's voice and the whole timeline.** This is the skeleton of the piece; today there
  are only isolated stages. *(Partly addressed since: see the prologue in `.ai/handoff.md`.)*
- **Spatial-audio visualisation** - ripples, vibrations, the "vibe net" between people. It appears
  **more often in the script than the dancing does**, is the visual language running through the
  entire piece, and there is none of it in the project.
- **Totem / XP rewards.** There are combo levels, but no one-off achievement feedback like
  "you earned a Totem".
- **Era progression** (1964 → 1984 → 2016 → 2017 → 2024/2025).
- **The ground timeline and the global map.**

## 5. A disagreement about layout: triangle vs. timeline

> ✅ **Resolved 2026-09-01: changed to the script's side.** Six stages are now laid out along a
> **zigzag timeline** in date order, strung together by a 2-metre white line on the ground - exactly
> the "zigzagging timeline" of p. 2. Empty placeholder takes were created for the three eras with no
> footage (ancestors / 1964 / MIT). The section below is kept as the record of why. Details in
> `Dance Place Stages and Standing Spec.md` §0.

The script's several "venues" are **different eras and worlds along one timeline**, which the player
moves **forward** along; whereas the three implemented in the
[Dance Place spec](Dance%20Place%20Stages%20and%20Standing%20Spec.md) were **parallel song-select
spots** - an equilateral triangle, 24 m apart, symmetrical, with no one of them being "next".

They are not the same thing:

| | As implemented | The script |
|---|---|---|
| Layout | Equilateral triangle, equidistant | One (zigzag) line, in date order |
| What travel means | **Pick a dance** | **Move forward / fast-forward along the timeline** |
| Order | Irrelevant | Meaningful - it is the narrative order |

To match the script, the stages should be laid out **on a line**, and travel should become "moving
along the timeline".

**Whether to change this depends on whether the current phase is validating mechanics or converging
on the final form - undecided, recorded here for now.**

---

# Appendix: beat-by-beat synopsis

> The script's own text is in `Wage Love IFeel (2).pdf` beside this file, which is the authority.
> This is a digest for finding your way around it: what happens, on which page. Song lyrics are
> referenced by source and situation rather than transcribed.

## p. 1 - INT. VR WORLD - NIGHT

> **[Game mechanic note]**: the timeline advances **automatically and linearly** if the player
> stands still and watches it as an interactive documentary. They can also **teleport down the
> timeline** to speed it up, and **teleport to different areas** to visit the IFEL proofs of
> concept. This is explained in onboarding before entering the space.

We spawn in an **all-black space**. A **narrator** - an old Black woman - introduces the story of
IFEL as objects and logos appear around us, each digital asset showing its name and date above it,
with a **timeline string** connecting them on the ground.

The narrator introduces IFEL as a new way for festivals, concerts, live TV shows and parties to be
shared around the world. As she speaks, a **Montreux 180 video** appears large in the distance, then
a 180 of the **symphony**, then a video/3D model of *The Voice*, and finally a 360 of the **AWE
party**; a small crowd of **dancing NPCs** appears in front of each.

She then names the technologies - **AR glasses, spatial audio, immersive cameras, low latency
streaming** - and each appears as it is named, in front of the larger venue images. Two dozen AR
glasses pop in around you under their brand names (Meta, Snap, Samsung, Apple, Google), **vertical
and dancing to the music**.

On "spatial audio", a wave of spatial audio ripples out of the Montreux footage (imagine Toph
"seeing" vibrations in *Avatar*), the visuals moving with the audio and surrounding you.

On "immersive cameras", human-sized 3D models of the cameras actually used in the shoots appear -
Black Magic, Canon, Insta360 - **also dancing**.

On "low latency streaming", **fibreoptic cables** and **5G towers** appear and start dancing.

## p. 2

The narrator turns: the project was never only about the technology coming together. At its core it
was about bringing people together - about **co-creation**, about **healing**, about **Waging
Love**. To understand how these technologies come together you have to know where the project
started: with **a vision of the future**, and **that vision grew into a dream**.

As she names those soft technologies, 3D models of them dance in place of the physical ones:
abstract representations of **Unity, Co-Creation, Healing, and a fist with Wage Love**.

On "future", the objects slide away into the distance and the ground timeline **counts back to
1964**. As the IFEL objects recede we are standing at the beginning of a **zigzagging timeline**
that starts underneath our feet, marked with many dates.

A giant **YouTube screen** appears playing 1964 footage, in front of a 3D model of **Hitsville
USA / Motown**. It is still at a distance but ripples of spatial audio come out of it, sounding like
people dancing to Motown in a crowded club.

**Martha** sings the opening calls of *Dancing in the Street*. The waves are visual as well and
ripple toward you a few times, then stop. A spot on the ground glows in front of 1964, and you have
to run over to it for the experience to continue; entering that proximity triggers it in full.

## p. 3

*Dancing in the Street* plays in full. The same spatial-audio ripples reverberate around you, but
this time the vibrations move in **two directions** (though we stay in 1964). The music fades down
and the narrator returns: even this groove came from somewhere - **feel the groove and call up its
history**.

A **dance guide** appears in front of you: "a translucent, spirit looking character that is ageless,
genderless, almost like the smoke from a flame turned to life". It does some simple 60s bob moves
and shows you how to follow along. As you move your body (the controllers), it causes the vibrations
to flow back through time.

The narrator frames **movement itself as a technology** - one so sophisticated it is
indistinguishable from magic.

The waves of your movement reverberate back through time, and in the distance we see a journey: to
**sharecroppers in the South (1900s)**, then to **slavery before that (1700s)**, then to **Africans
and other tribes dancing before that**, with many dates across a global map on the ground. The old
volumetric dancers from the 2019 shoot are abstracted in with the later ancestors, though you do not
know it yet.

They are visible but far off, so they are not given too much spotlight - but **"their movements all
have a semblance of the same move you're doing"**. (Perhaps with the option to run over and dance
with them.)

You get an **XP point** for "**FEELING THE GROOVE**"; a little jingle plays and you receive a
**Totem** of the accomplishment. The dancers of the past flow back into the distance, the 60s move
past, and several TVs and entertainment technologies go by - a VCR, VHS tapes. Finally **1984** stops
in front of you.

The music transitions to the **1984 Motown 25th Anniversary Special** on YouTube, with a giant TV
appearing around the video (made spatial if possible through Viggle pull-outs), fading down to the
background as the narrator comes back in.

## p. 4

The narrator: technology changes but **a groove is eternal**, and even a remix is a technology
that translates the groove for a new generation.

Several 80s kids pop up and dance among the dancers from the show. **Perhaps we mix 2D Viggle videos
with 3D characters and NPCs.** We hear **Sultan**'s voice (or family voices) with a spatial
soundscape: growing up in Detroit his family had a VHS tape of the Motown 25th Anniversary Special
and watched it about a thousand times, pointing to different people when it got to the cities.

As Martha sings the city names, a **mini city model** appears - like a 3D version of a prom picture
backdrop, not highly detailed but enough to recognise the city, with its name on top. Other NPCs
appear here from *The Voice* and Burnersphere recordings. They do a new doo-wop move and the **dance
spirit** appears and guides you in it. **Hold it for 5 seconds** (maybe there is a bar that grows as
you move) and you get a new Totem reward - **DANCING THROUGH TIME**.

Sultan continues over the fade: he used to imagine people in all the cities dancing together; his
house was always full of love when they danced and he imagined that spreading all over the world.
They never had name-brand clothes and were made fun of for it, which is why he loved the line that
**it doesn't matter what you wear**. He remembers a children's show animation of kids holding hands
around the globe.

## p. 5

Out of a photo of young Sultan appears a thought bubble, and from it an animated 3D version of the
**Saban logo**, the kids on it dancing. The images move away as we move through time, and the dream
of people dancing around the world comes to life as **a blue spirit**, moving through different
levels of transparency.

The narrator: a child's dream is powerful - **fragile, but hard to kill**.

Different music vibes and songs move past like clouds of spatial audio. We move through 80s hip hop,
90s pop, 2000s club music, with 3D models of music technologies appearing - drum machines, walkmans,
CDs, smartphones, MP3s, the internet, YouTube.

The narrator: as new technologies change how we see, the dream changes too, driven by inner growth
and by a growing belief that it could one day come to life.

Passing 2006 to 2010 we see the dancing animations from *Bilal's Stand*, showing a Wacom tablet, HD
video and YouTube, then land in 2013. As Sultan's voice returns we hear spatial audio of different
events, and the atmospheres he describes bubble up around us as worlds.

The **Dream Spirit** moves between the different spaces and slightly changes the frequency of its
composition depending on the space, since different kinds of music are playing in each.

Sultan, on *8 Mile*: Eminem is talking about a metaphorical barrier as much as a physical one; in
Detroit it is not just streets but cultural barriers that divide different worlds, and rarely do
people from one world go into the other.

## p. 6

Several **mini worlds** appear with wall dividers between them: **inner-city Detroit, a mosque, a
Burner party, a queer voguing/ballroom party, a rock/rugby party, a jazz/oldies party**. Sultan
describes himself as someone who went between worlds, "a constant daywalker in another reality",
learning to adjust slightly to feel the vibe of each place.

The jazz party comes from the symphony test; the rock party from Montreux. **A minigame starts
here.**

The narrator invites you to try **staying in the groove while making it your own**.

The **"follow the groove" bar** comes back, along with the **Dance Instructor** (which may resemble
or be the Dream Spirit). Other NPCs may be dancing in each style.

The Dance Instructor pops up in front of the Hip Hop world and does some hip hop moves; you dance
with it and feel the groove until the meter fills and creates a **Hip Hop Totem**. Then you move to
all the others in a row, each slightly different: a **dabke** in the Muslim world, an **EDM two-step**
at the Burner party, **voguing** at the ballroom, making someone **crowdsurf** at the rock party,
**conducting** at the symphony.

After the minigames are complete in each space (perhaps they come sliding to you rather than you
running to them) they start mixing up one after another in random order.

Sultan: it could be lonely being the only person to see the fun and beauty in each of these worlds
and never seeing what it would be like if they all came together - so in 2013 he threw the first
**World's Collide party**.

## p. 7

Sultan: he invited people from all walks of life into the same space and it was epic - the Muslims
hanging with the Burners, the cousins partying with the rugby players - and he realised **there is
something to vibration**.

This brings on another minigame or animation sequence: vibrations and frequency animated or made
through spatial audio, moving along to the narration. Everyone was there because they "vibed" on a
similar frequency, and because they vibed with him it did not take much for them to realise they
vibed with each other.

Based on your actions, whatever mechanic exists for individual vibration/frequency is moved to each
of the groups and between them. Maybe the **Dream Spirit gets bigger or more opaque**. Some kind of
string or **"vibe net"** starts to intersect between people, connecting them.

Sultan: it was magical, like those old days at home dancing with the family - more than family, for
that one night a united tribe. He got a taste of a dream and wanted to revisit it.

**The Dream Spirit reaches its next level of evolution**, dancing in the midst of all the confluent
vibes. But the vibes slowly fade into the past as the timeline moves toward 2016. Over the next few
years he did several World's Collide meetups, even thought about making it an app - then it all
changed when he did VR for the first time. Several VR headsets and headlines come down the timeline:
Clouds Over Sidra, Waves of Grace, Driving While Black.

## p. 8

Sultan: VR could transport you and put you in community with other people's worlds to feel their
vibrations - it was even called an empathy machine because of it. He saw the potential, but
everything culturally grounded was being consumed by outdated business models, where what was made
had to serve the function of an old paradigm: make people feel sad so they donate, or market a new
movie. **It was all 2D thinking.** None of those models would be sustainable because they were not
native to the potential of the new technology; he wanted to know what that would look like, so the
experimenting started.

We see the 2016 YouTube storyteller lab and an acceptance email to the **2016 Oculus Launchpad**.
Then 2D videos of the dance experiments: dancing with Google Cardboard, duct-taped phones. **Here we
get a type of beat saber dance teacher who teaches us moves one by one that will be put together in
the next dance.** Spatialised audio vibrations accompany each 2D video.

Sultan: he looked at dances and movements from around the world - a martial arts master taught him
the push and pull of yin and yang; a Zimbabwe harvest dance, an Irish circle dance - then gaff-taped
cardboard headsets to his roommates' faces and made them do the dances. But it felt too stiff, so he
got local dancers; still unable to get a 360 camera, they gaff-taped three phones to a tripod
pointing in different directions and had them dance to different styles of songs.

## p. 9

The video switches and now you are inside a 360 video. They finally got a 360 camera and filmed
their first 360 test, which turned into a 360 film with his students in Detroit.

**2017** comes down the timeline with the ***When It All Changed*** film, and in front of it the
YouTube video of the students remixing the dance. Sultan: unfortunately they told him his dance was
lame and that they wouldn't get caught dead doing it - **"They had no compassion for the compassion
dance."** So he asked them to make it cool.

This part might be tricky: take the 2D video of the guys remixing the dance and transition to the
360 video as you learn the moves. **The spirit Dance Teacher becomes the students and the Dance
Teacher in the film. The Dream Spirit gets bigger / more opaque.**

The narrator: the film lived up to its name, and they performed it all over the world.

Here we get **Viggle videos** of people doing the **Wage Love dance** from all over the world, and
the **Reality Hack footage** from the Wage Love campaign video. They danced on in multiple countries
across different continents; the dream was close to being realised, but still one person at a time in
one device - and they all joined the **Realizistance**.

Several people do the **Realizistance countdown**, joining those in the 360 video: **3 fingers
represent the power of story, 2 fingers represent peace**...

## p. 10

...**1 finger represents Unity. The fist represents power.**

We move to MIT and the **MIT Media Lab 3D model** consumes us. The project moved to MIT, and there
the idea began to also incorporate the **ancestors**: a DNA test brought back the possibility of
imagining all of these people as part of a tribe, a reuniting of tribes that all spread around the
world but started from one woman.

We see the DNA results and news headlines of Lucy and the first woman in Ethiopia. **The skull
becomes a person and joins the dancing. The volumetric ancestors come down**, and from here all the
events have dancing ancestors moving us down the timeline.

Narrator: the people kept dancing, the tribe kept growing, the technologies got more sophisticated
and the parties got wilder - at one point people danced with holograms of themselves - and the whole
time the dream got closer to being realised. A global pandemic did not stop them; they used it as a
chance to go to the motherland, and **danced with the ancestors in the very slave castles that were
used to sell their ancestors, as a radical act of healing**.

## p. 11

Through 2022, N2Existence. Then Viggle Sultan teaches the **ancestor dances** in a 2024 performance,
adding to the choreography with the Dance Teacher Spirit ("first you do like this to call in Papa,
then like this to call in Emma...").

Through the people dancing in 2024 and 2025 to the Brussels party, the new ancestors and the 2025 AWE
show - and finally we have caught back up to where we started, with IFEL.

Narrator: after the technologies of the dance, of the ancestors, and of the tribe had been thoroughly
mastered, with over 10,000 hours of dancing to keep the dream alive, the project was pitched to IBC
for the IFEL accelerator. The company logo 3D models come around and join the dance. The narrator
closes the loop: those technologies built **the experience you are standing in now, and your
decision to dance is what keeps the dream alive**.

## p. 12

Narrator: imagine it at a concert, at Coachella, at *The Voice*, where the live pyro can connect to
the tribalverse, where you can play interactive games, dance with people from all over the world,
bring your friends to a club in a different city - **and where they are is no longer a barrier to
feeling their presence with you**.

All the IFEL worlds come together in one giant party; *Diamonds* by Rihanna starts. We also see
footage of war and negative headlines around us.

The closing narration turns: this is not only dancing and entertainment - the world is **on a
precipice of darkness**, and today's technologies and business models feed it, but everyone has the
power to answer it with their own light. The project started with dance and aims far deeper, at
bringing people together; it was never really about IFEL. It ends on **"We-Feel"** and a call to the
**Realizistance**.

At the party we can move through the videos as a short film, and at the end of it we end up in the
room.
