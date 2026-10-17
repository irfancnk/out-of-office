const EMAIL = "ikaleli@gotobed.win";
const PLAYTEST = `mailto:${EMAIL}?subject=${encodeURIComponent("Out of Office playtest")}&body=${encodeURIComponent("Hi! I'd like to playtest Out of Office.\n\nMy phone: \n")}`;

const shot = (name, alt, sizes, eager = false) => `<img
  src="/img/${name}-720.jpg"
  srcset="/img/${name}-720.jpg 720w, /img/${name}-1290.jpg 1290w"
  sizes="${sizes}"
  width="1290" height="2796"
  alt="${alt}"
  ${eager ? 'fetchpriority="high"' : 'loading="lazy"'}
  decoding="async">`;

const phone = (finish, img) => `<div class="phone ${finish}"><div class="bezel"><div class="glass">${img}</div></div></div>`;

const tick = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12.5l4.5 4.5L19 7"/></svg>`;

const ticker = [
  "Auto-reply: I am currently out of office",
  "Expected back: never",
  "Coming to iPhone and Android",
  "64 rooms between you and your bed",
  "Three coffees per room",
  "Please do not circle back",
];

const exhibits = [
  {
    name: "home",
    finish: "orange",
    label: "Exhibit A · Home",
    title: "The lobby",
    line: "Rooms, wardrobe, daily tasks, and a very sleepy employee.",
    alt: "Out of Office home screen with the Level 15 button and tabs for rooms, wardrobe, home, tasks, and settings.",
  },
  {
    name: "badge",
    finish: "blue",
    label: "Exhibit B · Level 42",
    title: "Curve into the badge reader",
    line: "“Badge, curve, bed. Easy. Said someone once.”",
    alt: "Level 42. A curved wall guides the shot past a badge reader toward the bed, with three coffee cups on the floor.",
  },
  {
    name: "bridge",
    finish: "silver",
    label: "Exhibit C · Level 8",
    title: "Cross the bridge in the middle",
    line: "“Another room. Another reason to lie down.”",
    alt: "Level 8. Two round office rooms joined by a wooden bridge, with the bed in the far room.",
  },
];

const floors = [
  { name: "Open-Plan Office", range: "Levels 01–11", sign: "Everyone is very busy appearing busy.", levels: ["Onboarding", "Hot Desking", "The Boss's Chair", "Cubicle Farm"] },
  { name: "Office Kitchen", range: "Levels 12–20", sign: "The coffee machine is the real department head.", levels: ["Swivel Chairs", "Air Con Wars", "Badge Reader", "Fridge Raid"] },
  { name: "Meeting Room", range: "Levels 21–29", sign: "A meeting to discuss why there are too many meetings.", levels: ["Mail Chute", "Coffee Gravity", "Wet Floor", "Daily Standup"] },
  { name: "Manager's Office", range: "Levels 30–37", sign: "Urgent feedback that clarifies nothing.", levels: ["Budget Gaps", "Form 27B", "Spilled Latte", "360 Feedback"] },
  { name: "HR & Admin", range: "Levels 38–45", sign: "Submit the form requesting permission to submit the form.", levels: ["Spill Leap", "Printer Queue", "Beanbag Zone", "Paperwork"] },
  { name: "Executive Floor", range: "Levels 46–54", sign: "“We are all in this together.” Printed on a very expensive wall.", levels: ["Mail Belt", "Red Tape", "Executive Elevator", "Executive Putting Green"] },
  { name: "After Hours", range: "Levels 55–64", sign: "The building has finally stopped asking questions.", levels: ["Synergy", "All-Hands", "The Cleaner", "Lights Out"] },
];

const incidents = [
  { name: "The boss's chair", line: "Sends everyone back. Nothing personal.", color: "#ffe36e", r: -3 },
  { name: "Wet floor", line: "The sign was technically there.", color: "#a8dcff", r: 2.5 },
  { name: "Mail chute", line: "Faster than the IT ticket.", color: "#ffc2d8", r: -1.5 },
  { name: "Air con", line: "The air con has opinions.", color: "#c4f0c8", r: 3 },
  { name: "Standing desks", line: "They stand when you least expect it.", color: "#ffc98f", r: 2 },
  { name: "Spilled latte", line: "Productivity is spiralling.", color: "#ffe36e", r: -2.5 },
  { name: "One-way doors", line: "Like my career path.", color: "#d9cbff", r: 1.5 },
  { name: "Floor staples", line: "HR is aware.", color: "#a8dcff", r: -3.5 },
];

const specs = [
  { big: "0", label: "Art or audio files", text: "Every mesh, texture, sprite, and sound effect is generated in code. The only imported assets are fonts." },
  { big: "64/64", label: "Levels proven solvable", text: "A bot replays every level with the real game physics, golf style, and works out par before anything ships." },
  { big: "15k", label: "Lines of C#", text: "About 13,800 for the game itself and 1,700 for automated level, physics, and presentation checks." },
  { big: "22", label: "Mechanics", text: "Portals, ice, magnets, conveyors, wind, one-way doors, breakable walls, switches, and more." },
  { big: "80+", label: "Hand-written lines", text: "Every room has its own line of dry office humour. So do failures, coffees, and hole-in-ones." },
  { big: "1", label: "Thumb required", text: "Reduce motion, separate sound and music, undo, and native haptics on iPhone." },
];

const status = [
  { col: "Shipped", items: ["64 levels across 7 floors", "Wardrobe, quests, and rewards", "iPhone build with native haptics", "Automated level and physics checks"] },
  { col: "In progress", items: ["Playtesting with real players", "Android build", "Balancing and polish"] },
  { col: "Next", items: ["Soft launch", "App Store and Google Play release"] },
];

const tags = [
  { name: "Colors", from: "Mango Smoothie", to: "Promoted" },
  { name: "Hats", from: "Team Building Cap", to: "IT Wizard" },
  { name: "Face stickers", from: "Middle Management", to: "Employee of the Month" },
];

const faqs = [
  ["When does it come out?", "We're still building and playtesting. Launch timing will be announced here first."],
  ["Which phones will it run on?", "iPhone and Android. Both share the same levels, wardrobe, and progress system."],
  ["How do I join the playtest?", `Email <a href="${PLAYTEST}">${EMAIL}</a> and tell us which phone you use. We're inviting players in small groups.`],
  ["Do I need to be online?", "The game is being built to play without a connection. Daily quests reset at local midnight on your phone."],
  ["I'm from a startup program, publisher, or the press.", `Wonderful. Write to <a href="mailto:${EMAIL}">${EMAIL}</a> and we'll send a build, a short deck, and anything else you need.`],
];

const html = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>Out of Office: a one-thumb office puzzle for iPhone and Android</title>
<meta name="description" content="Out of Office is a one-thumb physics puzzle in development for iPhone and Android. 64 handmade rooms across 7 office floors. Collect coffee, survive the office, and reach the bed.">
<meta name="theme-color" content="#24104a">
<link rel="canonical" href="https://gotobed.win/">
<link rel="icon" href="/favicon.png" type="image/png">
<link rel="apple-touch-icon" href="/apple-touch-icon.png">
<meta property="og:type" content="website">
<meta property="og:site_name" content="Out of Office">
<meta property="og:title" content="Out of Office: coming to iPhone and Android">
<meta property="og:description" content="Collect coffee. Survive the office. Reach the bed.">
<meta property="og:url" content="https://gotobed.win/">
<meta property="og:image" content="https://gotobed.win/img/home-720.jpg">
<meta property="og:image:width" content="720">
<meta property="og:image:height" content="1560">
<meta name="twitter:card" content="summary_large_image">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,400;12..96,500;12..96,600;12..96,700;12..96,800&family=Caveat:wght@600;700&family=IBM+Plex+Mono:wght@400;500;600&family=Luckiest+Guy&display=swap" rel="stylesheet">
<link rel="preload" as="image" href="/img/home-720.jpg" imagesrcset="/img/home-720.jpg 720w, /img/home-1290.jpg 1290w" imagesizes="(min-width: 960px) 290px, 62vw">
<script>document.documentElement.classList.add("js")</script>
<style>
:root {
  color-scheme: light;
  --ink: #1d1330;
  --muted: #5f566f;
  --line: rgba(29, 19, 48, .14);
  --paper: #f3ede2;
  --sheet: #fffdf8;
  --purple: #3a1a6e;
  --deep: #24104a;
  --orange: #ff8a1f;
  --ink-red: #d8432a;
  --blue: #1d3f91;
  --mono: "IBM Plex Mono", ui-monospace, Menlo, monospace;
  --hand: Caveat, "Comic Sans MS", cursive;
  --head: "Bricolage Grotesque", system-ui, sans-serif;
  --wrap: 1200px;
  --ease: cubic-bezier(.2, .8, .2, 1);
}
* { box-sizing: border-box; }
html { scroll-behavior: smooth; -webkit-text-size-adjust: 100%; }
body {
  margin: 0;
  color: var(--ink);
  background-color: var(--paper);
  background-image: radial-gradient(rgba(29,19,48,.11) 1px, transparent 1.2px);
  background-size: 22px 22px;
  font: 400 17px/1.6 var(--head);
  -webkit-font-smoothing: antialiased;
  overflow-x: hidden;
}
img { display: block; max-width: 100%; height: auto; }
a { color: inherit; }
button { font: inherit; color: inherit; }
.wrap { width: min(var(--wrap), calc(100% - 40px)); margin: 0 auto; }
.skip { position: absolute; left: -999px; top: 8px; }
.skip:focus { left: 12px; z-index: 60; background: #fff; padding: 8px 12px; border-radius: 8px; }
:focus-visible { outline: 3px solid var(--orange); outline-offset: 3px; border-radius: 6px; }
.mono { font-family: var(--mono); }

.js [data-reveal] { opacity: 0; transform: translateY(26px); transition: opacity .8s var(--ease), transform .8s var(--ease); transition-delay: var(--d, 0s); }
.js [data-reveal].in { opacity: 1; transform: none; }

/* Ticker and nav */
.ticker { background: var(--deep); color: #ffd3a3; overflow: hidden; white-space: nowrap; font: 500 12px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; }
.ticker .track { display: inline-flex; padding: 11px 0; animation: marquee 48s linear infinite; }
.ticker span { padding: 0 22px; }
.ticker span::after { content: "✶"; margin-left: 44px; color: var(--orange); }
@keyframes marquee { to { transform: translateX(-50%); } }
.progress { position: fixed; top: 0; left: 0; right: 0; height: 3px; z-index: 40; background: var(--orange); transform-origin: 0 50%; transform: scaleX(0); }
.nav { position: sticky; top: 0; z-index: 30; background: rgba(243, 237, 226, .86); backdrop-filter: blur(14px) saturate(1.3); -webkit-backdrop-filter: blur(14px) saturate(1.3); border-bottom: 1px solid transparent; transition: border-color .25s; }
.nav.scrolled { border-bottom-color: var(--line); }
.nav .wrap { display: flex; align-items: center; gap: 24px; height: 66px; }
.brand { display: flex; align-items: center; gap: 10px; text-decoration: none; font-weight: 800; font-size: 18px; letter-spacing: -0.02em; }
.brand img { width: 34px; height: 34px; border-radius: 9px; box-shadow: 0 0 0 1.5px var(--ink); transition: transform .4s var(--ease); }
.brand:hover img { transform: rotate(-10deg); }
.nav-links { display: flex; gap: 2px; margin-left: auto; font: 500 12.5px/1 var(--mono); letter-spacing: .06em; text-transform: uppercase; }
.nav-links a { padding: 9px 11px; border-radius: 8px; text-decoration: none; color: var(--muted); transition: color .2s, background .2s; }
.nav-links a b { color: var(--orange); font-weight: 600; margin-right: 4px; }
.nav-links a:hover, .nav-links a.active { color: var(--ink); background: rgba(29,19,48,.06); }
.btn {
  display: inline-flex; align-items: center; justify-content: center; gap: 10px;
  min-height: 50px; padding: 0 22px; border-radius: 12px; border: 0; cursor: pointer;
  font: 700 16px/1 var(--head); text-decoration: none; white-space: nowrap; letter-spacing: -0.01em;
  transition: transform .18s var(--ease), box-shadow .18s, background .18s;
}
.btn-ink { background: var(--ink); color: #fff; box-shadow: 4px 4px 0 var(--orange); }
.btn-ink:hover { transform: translate(-2px, -2px); box-shadow: 6px 6px 0 var(--orange); }
.btn-ink:active { transform: translate(2px, 2px); box-shadow: 1px 1px 0 var(--orange); }
.btn-line { background: var(--sheet); color: var(--ink); box-shadow: inset 0 0 0 2px var(--ink); }
.btn-line:hover { background: #fff; transform: translateY(-2px); }
.nav .btn { min-height: 40px; padding: 0 16px; font-size: 14px; box-shadow: 3px 3px 0 var(--orange); }

/* Hero */
.hero { position: relative; overflow: hidden; }
.hero .wrap { display: grid; gap: 56px; align-items: center; padding: 56px 0 88px; }
.mail { position: relative; background: var(--sheet); border-radius: 16px; box-shadow: 0 0 0 1.5px var(--ink), 10px 10px 0 rgba(29,19,48,.9); }
.mail-head { display: grid; grid-template-columns: 82px 1fr; gap: 6px 10px; padding: 18px 26px; border-bottom: 1.5px dashed var(--line); font: 400 13px/1.5 var(--mono); color: var(--muted); }
.mail-head dt { text-transform: uppercase; letter-spacing: .1em; font-size: 11px; padding-top: 2px; }
.mail-head dd { margin: 0; color: var(--ink); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.mail-head .auto { display: inline-block; margin-right: 6px; padding: 1px 7px; border-radius: 5px; background: #ffe7cf; color: #a24b05; font-size: 11px; font-weight: 600; letter-spacing: .06em; text-transform: uppercase; }
.mail-body { padding: 30px 26px 30px; }
.wordmark { margin: 0; font: 400 clamp(64px, 8.6vw, 112px)/.86 "Luckiest Guy", "Arial Rounded MT Bold", sans-serif; letter-spacing: .01em; color: #fff; -webkit-text-stroke: clamp(4px, .55vw, 7px) var(--deep); paint-order: stroke fill; text-shadow: 0 .06em 0 var(--deep); }
.wordmark .w { display: inline-block; }
.wordmark .line2 { display: block; color: var(--orange); }
.js .wordmark .w { animation: pop .9s var(--ease) both; animation-delay: var(--d); }
@keyframes pop { 0% { opacity: 0; transform: translateY(40%) scale(.8) rotate(-5deg); } 60% { opacity: 1; transform: translateY(-6%) scale(1.04); } 100% { transform: none; } }
.lead { margin: 24px 0 0; font-size: clamp(22px, 2.3vw, 28px); line-height: 1.22; font-weight: 700; letter-spacing: -0.025em; max-width: 24ch; }
.sub { margin: 14px 0 0; max-width: 46ch; color: var(--muted); font-size: 17px; }
.sign { margin: 18px 0 0; font: 700 26px/1 var(--hand); color: var(--purple); transform: rotate(-2deg); transform-origin: 0 50%; }
.actions { display: flex; flex-wrap: wrap; gap: 14px; margin-top: 26px; }
.stamp {
  position: absolute; right: -26px; top: -30px; z-index: 2; pointer-events: none;
  padding: 10px 14px 9px; border: 3px solid var(--ink-red); border-radius: 10px; color: var(--ink-red);
  font: 600 14px/1.25 var(--mono); letter-spacing: .1em; text-transform: uppercase; text-align: center;
  box-shadow: inset 0 0 0 2px var(--sheet), inset 0 0 0 4px var(--ink-red);
  transform: rotate(-12deg); opacity: .86; mix-blend-mode: multiply;
}
.stamp b { display: block; font-size: 19px; letter-spacing: .06em; }
.js .stamp { animation: thunk .55s .95s var(--ease) both; }
@keyframes thunk { 0% { opacity: 0; transform: rotate(-12deg) scale(2.4); } 65% { opacity: .9; transform: rotate(-12deg) scale(.94); } 100% { opacity: .86; transform: rotate(-12deg) scale(1); } }

.desk { position: relative; justify-self: center; width: min(100%, 440px); padding: 40px 0 10px; }
.desk .holder { position: relative; width: 70%; margin-left: auto; transition: transform .6s var(--ease); }
.js .desk .holder { animation: rise 1.1s .2s var(--ease) both; }
@keyframes rise { from { opacity: 0; transform: translateY(60px); } to { opacity: 1; transform: none; } }
.lanyard { position: absolute; z-index: 3; left: 2%; top: -260px; width: 176px; transform-origin: 50% 0; animation: swing 4.6s ease-in-out infinite alternate; }
.lanyard:hover { animation-duration: 1.2s; }
@keyframes swing { from { transform: rotate(-5deg); } to { transform: rotate(5deg); } }
.strap { width: 22px; height: 400px; margin: 0 auto; background: repeating-linear-gradient(180deg, var(--purple) 0 54px, var(--orange) 54px 58px); border-radius: 0 0 4px 4px; box-shadow: inset 2px 0 0 rgba(255,255,255,.15); }
.clip { width: 30px; height: 18px; margin: -4px auto 0; border-radius: 4px; background: linear-gradient(180deg, #d9dce2, #9aa0a9); box-shadow: inset 0 0 0 1px rgba(0,0,0,.15); }
.badge { margin-top: -2px; padding: 14px 14px 12px; border-radius: 14px; background: #fff; box-shadow: 0 0 0 1.5px var(--ink), 0 24px 30px -18px rgba(29,19,48,.55); }
.badge .hole { width: 38px; height: 7px; margin: 0 auto 10px; border-radius: 6px; background: var(--paper); box-shadow: inset 0 1px 2px rgba(0,0,0,.25); }
.badge .co { padding: 5px 8px; border-radius: 6px; background: var(--deep); color: #ffd3a3; font: 600 9.5px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; text-align: center; }
.badge img { width: 74px; height: 74px; margin: 12px auto 10px; border-radius: 16px; box-shadow: 0 0 0 1.5px var(--ink); }
.badge .who { text-align: center; font-weight: 800; font-size: 16px; letter-spacing: -0.01em; line-height: 1.1; }
.badge .role { text-align: center; font: 400 10.5px/1.4 var(--mono); color: var(--muted); text-transform: uppercase; letter-spacing: .08em; margin-top: 4px; }
.badge .access { margin: 10px 0 8px; padding: 4px; border-radius: 6px; background: #ffe7cf; color: #a24b05; font: 600 10px/1.2 var(--mono); letter-spacing: .12em; text-align: center; text-transform: uppercase; }
.barcode { height: 24px; background: repeating-linear-gradient(90deg, var(--ink) 0 2px, transparent 2px 3px, var(--ink) 3px 4px, transparent 4px 7px, var(--ink) 7px 10px, transparent 10px 12px); border-radius: 2px; }
.sticky { position: absolute; z-index: 4; padding: 14px 16px 18px; background: #ffe36e; font: 700 24px/1.05 var(--hand); color: #3b2a00; box-shadow: 0 14px 20px -12px rgba(0,0,0,.45); transform: rotate(5deg); }
.sticky::before { content: ""; position: absolute; left: 50%; top: -9px; width: 54px; height: 18px; transform: translateX(-50%) rotate(-3deg); background: rgba(255,255,255,.55); box-shadow: 0 0 0 1px rgba(0,0,0,.04); }
.desk .sticky { right: -14px; bottom: 54px; width: 140px; }
.ring { position: absolute; z-index: 0; width: 150px; height: 150px; left: 4%; bottom: -10px; border-radius: 50%; border: 9px solid rgba(140, 86, 40, .13); box-shadow: inset 0 0 0 3px rgba(140, 86, 40, .06); filter: blur(.4px); }

/* iPhone 17 Pro Max */
.phone {
  --frame: linear-gradient(135deg, #ffb878 0%, #e8732a 28%, #c4561a 62%, #f19a55 100%);
  --button: #c9601f;
  position: relative; padding: 1.45%; border-radius: 15.6% / 7.3%; background: var(--frame);
  box-shadow: inset 0 0 0 1px rgba(255,255,255,.35), inset 0 2px 1px rgba(255,255,255,.35), inset 0 -2px 3px rgba(0,0,0,.28), 0 50px 80px -36px rgba(36,16,74,.6), 0 22px 36px -22px rgba(0,0,0,.45);
}
.phone.blue { --frame: linear-gradient(135deg, #6c80aa 0%, #34446a 30%, #1f2a46 64%, #56699a 100%); --button: #2a3756; }
.phone.silver { --frame: linear-gradient(135deg, #ffffff 0%, #d7dae0 30%, #b3b8c0 64%, #eceef1 100%); --button: #a9aeb6; }
.phone::before, .phone::after { content: ""; position: absolute; width: 1.1%; border-radius: 3px; }
.phone::before { left: -.9%; top: 15%; height: 24%; background: linear-gradient(to bottom, var(--button) 0 13%, transparent 13% 25%, var(--button) 25% 58%, transparent 58% 66%, var(--button) 66% 100%); }
.phone::after { right: -.9%; top: 24%; height: 38%; background: linear-gradient(to bottom, var(--button) 0 28%, transparent 28% 74%, var(--button) 74% 100%); }
.bezel { border-radius: 14.4% / 6.75%; background: #040406; padding: 2.4%; box-shadow: inset 0 0 0 1px rgba(255,255,255,.07); }
.glass { position: relative; border-radius: 12.2% / 5.65%; overflow: hidden; background: #111; isolation: isolate; }
.glass img { width: 100%; }
.glass::before { content: ""; position: absolute; z-index: 2; top: 1.3%; left: 50%; transform: translateX(-50%); width: 29%; height: 3.4%; border-radius: 999px; background: radial-gradient(circle at 82% 50%, #26304f 0 7%, #0c0f1c 8% 13%, transparent 14%), #000; }
.glass::after { content: ""; position: absolute; inset: 0; z-index: 1; pointer-events: none; background: linear-gradient(118deg, rgba(255,255,255,.18) 0%, rgba(255,255,255,.04) 26%, rgba(255,255,255,0) 40%); }

/* Shared section heads */
section { scroll-margin-top: 70px; }
.section { padding: 112px 0; }
.kicker { display: flex; align-items: center; gap: 12px; margin: 0 0 16px; font: 500 12.5px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; color: var(--muted); }
.kicker b { color: var(--orange); font-weight: 600; }
.kicker::after { content: ""; flex: 0 0 60px; height: 1.5px; background: currentColor; opacity: .4; }
h2 { margin: 0; font: 800 clamp(38px, 5.4vw, 68px)/1 var(--head); letter-spacing: -0.04em; }
h2 em { font: 700 1.05em/1 var(--hand); letter-spacing: 0; color: var(--orange); font-style: normal; }
.head { display: grid; gap: 18px; margin-bottom: 56px; }
.head p { margin: 0; max-width: 56ch; color: var(--muted); font-size: 18px; }

/* Job posting */
.posting { position: relative; background: var(--sheet); border-radius: 6px; box-shadow: 0 0 0 1.5px var(--ink), 0 40px 60px -50px rgba(29,19,48,.6); padding: clamp(28px, 5vw, 64px); }
.posting::before { content: ""; position: absolute; top: 0; bottom: 0; left: clamp(14px, 2.4vw, 30px); width: 2px; background: rgba(216, 67, 42, .35); }
.posting .ref { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px 24px; padding-bottom: 18px; margin-bottom: 30px; border-bottom: 1.5px solid var(--ink); font: 500 12px/1.4 var(--mono); letter-spacing: .12em; text-transform: uppercase; color: var(--muted); }
.posting h2 { font-size: clamp(34px, 4.6vw, 58px); }
.posting .summary { margin: 18px 0 0; max-width: 60ch; font-size: 19px; color: var(--muted); }
.cols { display: grid; gap: 36px; margin-top: 44px; }
.cols h3 { margin: 0 0 16px; font: 600 12px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; color: var(--orange); }
.duties { margin: 0; padding: 0; list-style: none; display: grid; gap: 20px; }
.duties li { display: grid; grid-template-columns: 40px 1fr; gap: 14px; }
.duties .n { display: grid; place-items: center; width: 40px; height: 40px; border-radius: 10px; background: var(--ink); color: #fff; font: 600 14px/1 var(--mono); transition: transform .3s var(--ease), background .3s; }
.duties li:hover .n { transform: rotate(-8deg); background: var(--orange); }
.duties strong { display: block; font-size: 19px; letter-spacing: -0.015em; }
.duties span { color: var(--muted); font-size: 16px; }
.boxes { margin: 0; padding: 0; list-style: none; display: grid; gap: 12px; }
.boxes li { display: grid; grid-template-columns: 24px 1fr; gap: 12px; font-size: 17px; }
.boxes svg { width: 22px; height: 22px; padding: 2px; border-radius: 5px; box-shadow: inset 0 0 0 1.5px var(--ink); color: var(--purple); }
.terms { display: grid; grid-template-columns: repeat(2, 1fr); margin: 48px 0 0; padding: 0; list-style: none; border-top: 1.5px solid var(--ink); }
.terms li { padding: 22px 6px 0; }
.terms b { display: block; font: 400 clamp(44px, 5vw, 64px)/1 "Luckiest Guy", sans-serif; color: var(--purple); }
.terms span { display: block; margin-top: 6px; font: 500 12px/1.4 var(--mono); letter-spacing: .1em; text-transform: uppercase; color: var(--muted); }

/* Elevator */
.lobby { display: grid; gap: 30px; align-items: stretch; }
.elevator { justify-self: center; width: min(100%, 300px); padding: 24px 24px 28px; border-radius: 22px; background: linear-gradient(100deg, #b9bdc5 0%, #eef0f3 38%, #d3d6dc 62%, #aeb3bb 100%); box-shadow: 0 0 0 1.5px var(--ink), inset 0 1px 0 #fff, 0 30px 50px -36px rgba(29,19,48,.7); }
.display { display: flex; justify-content: space-between; align-items: center; padding: 14px 20px; border-radius: 10px; background: #160c05; box-shadow: inset 0 0 0 2px #000, inset 0 0 18px rgba(255,120,30,.25); color: #ff9a3c; text-shadow: 0 0 10px rgba(255,140,40,.8); font: 600 44px/1 var(--mono); }
.display .arrow { font-size: 26px; opacity: .25; transition: opacity .2s; }
.display .arrow.on { opacity: 1; }
.display.ding { animation: ding .6s; }
@keyframes ding { 30% { box-shadow: inset 0 0 0 2px #000, inset 0 0 40px rgba(255,150,50,.7); } }
.plate { margin: 16px 0 18px; text-align: center; font: 600 10.5px/1 var(--mono); letter-spacing: .2em; text-transform: uppercase; color: #5b606a; }
.buttons { display: grid; grid-template-columns: repeat(2, 64px); justify-content: center; gap: 16px 26px; direction: rtl; }
.ebtn { direction: ltr; width: 64px; height: 64px; border-radius: 50%; border: 0; cursor: pointer; background: radial-gradient(circle at 35% 30%, #ffffff, #c7cbd2 70%); box-shadow: 0 0 0 3px #9ea3ab, 0 0 0 5px #e7e9ed, 0 6px 10px -4px rgba(0,0,0,.4); font: 600 22px/1 var(--mono); color: #3d424b; transition: box-shadow .25s, color .25s, transform .12s; }
.ebtn:hover { transform: translateY(-1px); }
.ebtn:active { transform: scale(.95); }
.ebtn[aria-pressed="true"] { color: #ff7a00; box-shadow: 0 0 0 3px #ff9a3c, 0 0 18px 4px rgba(255,138,31,.55), 0 0 0 5px #e7e9ed, 0 6px 10px -4px rgba(0,0,0,.4); text-shadow: 0 0 8px rgba(255,138,31,.7); }
.ebtn:last-child { grid-column: 1 / -1; justify-self: center; }
.directory {
  position: relative; padding: clamp(26px, 4vw, 46px); border-radius: 14px; color: #f2eee6;
  background-color: #1b1a20;
  background-image: repeating-linear-gradient(180deg, rgba(255,255,255,.045) 0 1px, transparent 1px 24px);
  box-shadow: 0 0 0 12px #4a3323, 0 0 0 13.5px var(--ink), 0 40px 60px -40px rgba(29,19,48,.8);
  font-family: var(--mono); text-transform: uppercase; letter-spacing: .12em;
  min-height: 360px; margin: 14px;
}
.directory .row1 { display: flex; justify-content: space-between; gap: 12px; font-size: 12px; color: #b9b2a5; }
.directory h3 { margin: 18px 0 0; font: 600 clamp(28px, 3.6vw, 44px)/1.08 var(--mono); letter-spacing: .06em; color: #fff; }
.directory blockquote { margin: 18px 0 0; max-width: 40ch; font-size: 14.5px; line-height: 1.6; color: #e7dfcf; letter-spacing: .08em; }
.directory blockquote::before { content: "Notice  "; color: var(--orange); }
.directory ul { margin: 26px 0 0; padding: 0; list-style: none; display: grid; gap: 8px; font-size: 14px; }
.directory li { display: flex; gap: 10px; align-items: baseline; }
.directory li::after { content: ""; flex: 1; order: 1; border-bottom: 2px dotted rgba(255,255,255,.25); transform: translateY(-4px); }
.directory li span { order: 0; }
.directory li i { order: 2; font-style: normal; color: #b9b2a5; font-size: 12px; }
.flip { animation: flip .5s var(--ease); }
@keyframes flip { from { opacity: 0; transform: translateY(8px); filter: blur(3px); } to { opacity: 1; transform: none; filter: none; } }

/* Corkboard */
.cork {
  padding: clamp(28px, 4vw, 52px) clamp(20px, 3vw, 44px) clamp(36px, 4vw, 60px);
  border-radius: 14px; background-color: #c99a62;
  background-image: radial-gradient(rgba(70,40,10,.22) 1px, transparent 1.6px), radial-gradient(rgba(255,240,210,.18) 1px, transparent 1.6px);
  background-size: 7px 7px, 11px 11px; background-position: 0 0, 3px 5px;
  box-shadow: inset 0 0 50px rgba(60,30,5,.35), 0 0 0 14px #7a4f2c, 0 0 0 15.5px var(--ink), 0 40px 60px -40px rgba(29,19,48,.7);
  margin: 16px;
}
.notes { display: grid; grid-template-columns: repeat(2, 1fr); gap: clamp(18px, 2.4vw, 30px); margin: 0; padding: 0; list-style: none; }
.note { position: relative; padding: 26px 18px 22px; min-height: 176px; background: var(--n); box-shadow: 0 16px 18px -14px rgba(0,0,0,.55), inset 0 -30px 30px -30px rgba(0,0,0,.1); transform: rotate(var(--r)); transition: transform .35s var(--ease), box-shadow .35s; }
.note::before { content: ""; position: absolute; top: -7px; left: 50%; width: 18px; height: 18px; margin-left: -9px; border-radius: 50%; background: radial-gradient(circle at 35% 30%, #ff8b7a, #c4291a 70%); box-shadow: 0 4px 4px -1px rgba(0,0,0,.4); }
.note:hover { transform: rotate(0) translateY(-8px) scale(1.04); box-shadow: 0 28px 28px -18px rgba(0,0,0,.6); z-index: 2; }
.note .no { font: 600 10.5px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; opacity: .6; }
.note h3 { margin: 8px 0 6px; font-size: 19px; line-height: 1.15; letter-spacing: -0.015em; }
.note p { margin: 0; font: 700 23px/1.05 var(--hand); color: #2e2a3a; }

/* Exhibits */
.plans { position: relative; background-color: var(--deep); color: #fff; overflow: hidden;
  background-image: linear-gradient(rgba(255,255,255,.055) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.055) 1px, transparent 1px);
  background-size: 44px 44px; }
.plans .kicker { color: rgba(255,255,255,.65); }
.plans .head p { color: rgba(255,255,255,.72); }
.exhibits { display: grid; gap: 44px; margin: 0; padding: 0; list-style: none; }
.exhibit button { position: relative; display: block; width: 100%; padding: 0; border: 0; background: none; cursor: zoom-in; border-radius: 15% / 7%; }
.exhibit .phone { transition: transform .5s var(--ease); box-shadow: inset 0 0 0 1px rgba(255,255,255,.35), inset 0 2px 1px rgba(255,255,255,.35), 0 44px 70px -34px rgba(0,0,0,.9); }
.exhibit button:hover .phone { transform: translateY(-10px) rotate(-1.2deg); }
.tape { position: absolute; z-index: 3; top: -14px; left: 50%; padding: 7px 16px; transform: translateX(-50%) rotate(-3deg); background: rgba(255, 238, 196, .92); color: #3b2a00; font: 600 11.5px/1 var(--mono); letter-spacing: .1em; text-transform: uppercase; white-space: nowrap; box-shadow: 0 4px 10px -4px rgba(0,0,0,.5); }
.exhibit:nth-child(2) .tape { transform: translateX(-50%) rotate(2deg); }
.exhibit h3 { margin: 24px 0 4px; font-size: 22px; letter-spacing: -0.02em; }
.exhibit p { margin: 0; color: rgba(255,255,255,.72); font-size: 15.5px; }
.exhibit .zoom { display: inline-block; margin-top: 12px; font: 500 11.5px/1 var(--mono); letter-spacing: .1em; text-transform: uppercase; color: #ffd3a3; }

/* Spec sheet */
.blueprint { background-color: var(--blue); color: #fff;
  background-image: linear-gradient(rgba(255,255,255,.08) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.08) 1px, transparent 1px), linear-gradient(rgba(255,255,255,.035) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.035) 1px, transparent 1px);
  background-size: 110px 110px, 110px 110px, 22px 22px, 22px 22px; }
.blueprint .kicker { color: rgba(255,255,255,.7); }
.blueprint .head p { color: rgba(255,255,255,.78); }
.specs { display: grid; grid-template-columns: 1fr; margin: 0; padding: 0; list-style: none; border: 1.5px solid rgba(255,255,255,.55); }
.spec { position: relative; padding: 30px 28px 32px; border-bottom: 1.5px solid rgba(255,255,255,.55); }
.spec:last-child { border-bottom: 0; }
.spec .idx { position: absolute; top: 14px; right: 16px; font: 500 11px/1 var(--mono); letter-spacing: .12em; color: rgba(255,255,255,.5); }
.spec b { display: block; font: 800 clamp(48px, 5.6vw, 76px)/1 var(--head); letter-spacing: -0.05em; }
.spec .label { display: block; margin-top: 10px; font: 600 12px/1.3 var(--mono); letter-spacing: .14em; text-transform: uppercase; color: #ffd3a3; }
.spec p { margin: 10px 0 0; font-size: 16px; color: rgba(255,255,255,.82); max-width: 34ch; }
.board { margin-top: 64px; }
.board h3 { margin: 0 0 20px; font: 600 12.5px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; color: rgba(255,255,255,.7); }
.lanes { display: grid; gap: 16px; }
.lane { padding: 18px; border-radius: 14px; background: rgba(255,255,255,.08); box-shadow: inset 0 0 0 1.5px rgba(255,255,255,.22); }
.lane h4 { display: flex; justify-content: space-between; margin: 0 0 14px; font: 600 12px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; }
.lane h4 i { font-style: normal; opacity: .6; }
.lane ul { margin: 0; padding: 0; list-style: none; display: grid; gap: 10px; }
.lane li { padding: 12px 14px; border-radius: 10px; background: #fff; color: var(--ink); font-size: 15px; font-weight: 600; box-shadow: 0 6px 12px -8px rgba(0,0,0,.6); border-left: 5px solid var(--c); transition: transform .25s var(--ease); }
.lane li:hover { transform: translateX(4px); }

/* Wardrobe */
.dress { display: grid; gap: 48px; align-items: start; }
.hangtags { display: grid; gap: 16px; margin: 30px 0 0; padding: 0; list-style: none; }
.hangtag { position: relative; display: grid; grid-template-columns: 1fr auto; gap: 4px 16px; align-items: center; padding: 18px 22px 18px 52px; background: var(--sheet); border-radius: 12px; box-shadow: 0 0 0 1.5px var(--ink), 4px 4px 0 rgba(29,19,48,.12); transition: transform .3s var(--ease); }
.hangtag::before { content: ""; position: absolute; left: 20px; top: 50%; width: 11px; height: 11px; margin-top: -5.5px; border-radius: 50%; box-shadow: inset 0 0 0 2px var(--ink); background: var(--paper); }
.hangtag:hover { transform: rotate(-1.5deg) translateX(4px); }
.hangtag strong { font-size: 18px; letter-spacing: -0.015em; }
.hangtag span { grid-column: 1; font: 400 13px/1.4 var(--mono); color: var(--muted); }
.hangtag em { grid-row: 1 / span 2; grid-column: 2; font: 700 24px/1 var(--hand); color: var(--purple); font-style: normal; }
.rarity { display: flex; flex-wrap: wrap; gap: 8px; margin: 20px 0 0; padding: 0; list-style: none; }
.rarity li { display: flex; align-items: center; gap: 8px; padding: 7px 12px; border-radius: 999px; background: var(--sheet); box-shadow: 0 0 0 1.5px var(--ink); font: 500 12px/1 var(--mono); letter-spacing: .08em; text-transform: uppercase; }
.rarity i { width: 10px; height: 10px; border-radius: 50%; background: var(--c); }
.clipboard { position: relative; justify-self: center; width: min(100%, 460px); padding: 34px 18px 18px; border-radius: 18px; background: linear-gradient(160deg, #b98250, #8f5b30); box-shadow: 0 0 0 1.5px var(--ink), 0 40px 60px -40px rgba(29,19,48,.8); transform: rotate(1.5deg); }
.clipboard::before { content: ""; position: absolute; top: -14px; left: 50%; width: 130px; height: 40px; margin-left: -65px; border-radius: 10px 10px 6px 6px; background: linear-gradient(180deg, #e6e8ec, #9ea4ad); box-shadow: 0 0 0 1.5px var(--ink), inset 0 -6px 0 rgba(0,0,0,.12); }
.sheet { padding: 30px 26px 26px; background: var(--sheet); border-radius: 6px; background-image: repeating-linear-gradient(180deg, transparent 0 33px, rgba(29,63,145,.14) 33px 34px); }
.sheet h3 { margin: 0; font: 700 34px/1 var(--hand); color: var(--purple); }
.sheet .small { margin: 6px 0 18px; font: 400 12.5px/1.5 var(--mono); color: var(--muted); }
.todo { margin: 0; padding: 0; list-style: none; }
.todo li { display: grid; grid-template-columns: 26px 1fr auto; gap: 12px; align-items: center; min-height: 34px; font-size: 17px; font-weight: 600; }
.todo .box { width: 22px; height: 22px; border-radius: 5px; box-shadow: inset 0 0 0 2px var(--ink); color: var(--ink-red); }
.todo .box svg { width: 22px; height: 22px; opacity: 0; transform: scale(.4) rotate(-20deg); transition: opacity .3s, transform .5s var(--ease); }
.todo li.done .box svg { opacity: 1; transform: none; }
.todo li.done .q { text-decoration: line-through; text-decoration-color: var(--ink-red); text-decoration-thickness: 2px; color: var(--muted); }
.todo em { font: 700 22px/1 var(--hand); color: var(--ink-red); font-style: normal; }
.shift { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-top: 18px; padding-top: 14px; border-top: 1.5px dashed var(--line); font: 500 12px/1 var(--mono); letter-spacing: .1em; text-transform: uppercase; color: var(--muted); }
.shift b { font: 600 20px/1 var(--mono); color: var(--ink); letter-spacing: .04em; font-variant-numeric: tabular-nums; }

/* Settings */
.split { display: grid; gap: 52px; align-items: center; }
.split .phone { width: min(100%, 290px); margin: 0 auto; transform: rotate(-3deg); }
.form { margin: 30px 0 0; padding: 0; list-style: none; border-top: 1.5px solid var(--ink); }
.form li { display: grid; grid-template-columns: 1fr auto; gap: 16px; align-items: center; padding: 18px 0; border-bottom: 1px solid var(--line); }
.form strong { display: block; font-size: 18px; letter-spacing: -0.01em; }
.form span { color: var(--muted); font-size: 15.5px; }
.toggle { position: relative; width: 50px; height: 30px; border-radius: 999px; border: 0; cursor: pointer; background: #d8d2c6; box-shadow: inset 0 0 0 1.5px var(--ink); transition: background .25s; }
.toggle::after { content: ""; position: absolute; top: 4px; left: 4px; width: 22px; height: 22px; border-radius: 50%; background: #fff; box-shadow: 0 0 0 1.5px var(--ink); transition: transform .3s var(--ease); }
.toggle[aria-checked="true"] { background: #5fd17a; }
.toggle[aria-checked="true"]::after { transform: translateX(20px); }

/* FAQ */
.faq { max-width: 860px; border-top: 1.5px solid var(--ink); }
.faq details { border-bottom: 1px solid var(--line); }
.faq summary { display: grid; grid-template-columns: 46px 1fr 34px; align-items: center; gap: 10px; padding: 22px 0; cursor: pointer; list-style: none; font-size: 20px; font-weight: 700; letter-spacing: -0.02em; }
.faq summary::-webkit-details-marker { display: none; }
.faq summary i { font: 500 12px/1 var(--mono); color: var(--orange); font-style: normal; letter-spacing: .1em; }
.faq summary::after { content: "+"; display: grid; place-items: center; width: 34px; height: 34px; border-radius: 9px; box-shadow: inset 0 0 0 1.5px var(--ink); font: 500 20px/1 var(--mono); transition: transform .35s var(--ease), background .2s; }
.faq details[open] summary::after { content: "–"; background: #ffe7cf; }
.faq p { margin: 0; padding: 0 44px 24px 56px; color: var(--muted); font-size: 17px; }
.faq a { color: var(--purple); font-weight: 700; }

/* Reception */
.reception { padding: 0 0 120px; }
.desk-card { display: grid; gap: 48px; align-items: center; padding: clamp(30px, 5vw, 64px); border-radius: 24px; background: var(--ink); color: #fff; box-shadow: 10px 10px 0 var(--orange); }
.desk-card h2 { color: #fff; }
.desk-card p { margin: 18px 0 0; max-width: 46ch; color: rgba(255,255,255,.78); font-size: 18px; }
.desk-card .actions { margin-top: 28px; }
.desk-card .btn-ink { background: var(--orange); color: var(--ink); box-shadow: 4px 4px 0 #fff; }
.desk-card .btn-ink:hover { box-shadow: 6px 6px 0 #fff; }
.desk-card .btn-line { background: transparent; color: #fff; box-shadow: inset 0 0 0 2px rgba(255,255,255,.6); }
.desk-card .btn-line:hover { box-shadow: inset 0 0 0 2px #fff; }
.card-stage { perspective: 1000px; justify-self: center; width: min(100%, 420px); }
.bizcard { position: relative; aspect-ratio: 1.75; padding: 26px 28px; border-radius: 14px; background: var(--sheet); color: var(--ink); box-shadow: 0 30px 50px -20px rgba(0,0,0,.6); transform: rotate(-4deg); transition: transform .5s var(--ease); display: grid; grid-template-rows: auto 1fr auto; }
.bizcard .top { display: flex; align-items: center; gap: 12px; }
.bizcard img { width: 46px; height: 46px; border-radius: 12px; box-shadow: 0 0 0 1.5px var(--ink); }
.bizcard .name { font: 400 26px/.9 "Luckiest Guy", sans-serif; color: var(--purple); }
.bizcard .title { font: 500 10.5px/1.4 var(--mono); letter-spacing: .12em; text-transform: uppercase; color: var(--muted); }
.bizcard .lines { align-self: end; font: 500 13.5px/1.7 var(--mono); }
.bizcard .lines span { color: var(--muted); }
.bizcard .hours { position: absolute; right: 22px; top: 24px; font: 700 22px/1 var(--hand); color: var(--ink-red); transform: rotate(6deg); }
.toast { position: fixed; left: 50%; bottom: 24px; z-index: 70; transform: translate(-50%, 20px); opacity: 0; padding: 12px 18px; border-radius: 10px; background: var(--ink); color: #fff; font: 500 13px/1 var(--mono); box-shadow: 4px 4px 0 var(--orange); transition: opacity .3s, transform .4s var(--ease); pointer-events: none; }
.toast.show { opacity: 1; transform: translate(-50%, 0); }

footer { border-top: 1.5px solid var(--ink); background: var(--sheet); }
footer .wrap { display: grid; gap: 24px; padding: 40px 0 48px; }
.autoreply { font: 400 14px/1.7 var(--mono); color: var(--muted); max-width: 70ch; }
.autoreply b { color: var(--ink); font-weight: 600; }
.foot { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 12px 24px; font: 500 12px/1.4 var(--mono); letter-spacing: .08em; text-transform: uppercase; color: var(--muted); }
.foot a { color: var(--ink); text-decoration: none; }

/* Viewer */
dialog { padding: 0; border: 0; background: transparent; max-width: none; max-height: none; width: 100vw; height: 100dvh; margin: 0; }
dialog::backdrop { background: rgba(14, 8, 26, .9); backdrop-filter: blur(8px); }
dialog[open] .viewer img { animation: zoomin .45s var(--ease); }
@keyframes zoomin { from { opacity: 0; transform: scale(.94); } to { opacity: 1; transform: none; } }
.viewer { display: grid; place-items: center; width: 100%; height: 100%; padding: 24px 72px; }
.viewer img { max-height: calc(100dvh - 64px); width: auto; border-radius: 34px; box-shadow: 0 30px 80px rgba(0,0,0,.55); }
.vbtn { position: fixed; display: grid; place-items: center; width: 48px; height: 48px; border-radius: 12px; border: 0; background: rgba(255,255,255,.14); color: #fff; cursor: pointer; transition: background .2s; }
.vbtn:hover { background: rgba(255,255,255,.26); }
.vbtn svg { width: 22px; height: 22px; }
.vclose { top: 18px; right: 18px; }
.vprev { left: 16px; top: 50%; margin-top: -24px; }
.vnext { right: 16px; top: 50%; margin-top: -24px; }
.vcount { position: fixed; left: 50%; bottom: 18px; transform: translateX(-50%); color: rgba(255,255,255,.7); font: 500 13px/1 var(--mono); letter-spacing: .1em; }

@media (min-width: 720px) {
  .terms { grid-template-columns: repeat(4, 1fr); }
  .notes { grid-template-columns: repeat(4, 1fr); }
  .exhibits { grid-template-columns: repeat(3, 1fr); }
  .specs { grid-template-columns: repeat(2, 1fr); }
  .spec { border-right: 1.5px solid rgba(255,255,255,.55); }
  .spec:nth-child(2n) { border-right: 0; }
  .spec:nth-last-child(-n+2) { border-bottom: 0; }
  .lanes { grid-template-columns: repeat(3, 1fr); }
  .cols { grid-template-columns: 1.4fr 1fr 1fr; gap: 48px; }
}
@media (min-width: 960px) {
  .hero .wrap { grid-template-columns: 1.08fr .92fr; padding: 64px 0 104px; }
  .lobby { grid-template-columns: 300px 1fr; gap: 56px; }
  .specs { grid-template-columns: repeat(3, 1fr); }
  .spec, .spec:nth-child(2n) { border-right: 1.5px solid rgba(255,255,255,.55); }
  .spec:nth-child(3n) { border-right: 0; }
  .spec:nth-last-child(-n+2) { border-bottom: 1.5px solid rgba(255,255,255,.55); }
  .spec:nth-last-child(-n+3) { border-bottom: 0; }
  .dress { grid-template-columns: 1fr 1fr; gap: 72px; }
  .split { grid-template-columns: .85fr 1.15fr; gap: 88px; }
  .desk-card { grid-template-columns: 1.1fr .9fr; }
}
@media (max-width: 900px) {
  .nav-links { display: none; }
  .nav .btn { margin-left: auto; }
}
@media (max-width: 760px) {
  .section { padding: 84px 0; }
  .mail-head { grid-template-columns: 64px 1fr; padding: 14px 18px; font-size: 12px; }
  .mail-body { padding: 24px 18px; }
  .stamp { top: -22px; right: 10px; font-size: 10px; padding: 7px 10px 6px; }
  .desk { clip-path: inset(0 -60px -60px -60px); padding-top: 24px; }
  .stamp b { font-size: 15px; }
  .lanyard { width: 140px; top: -310px; left: 0; }
  .desk .sticky { width: 120px; font-size: 21px; right: -6px; }
  .exhibits { grid-auto-flow: column; grid-auto-columns: 74%; overflow-x: auto; scroll-snap-type: x mandatory; padding: 24px 20px 8px 4px; margin: 0 -20px 0 -4px; scrollbar-width: none; }
  .exhibits::-webkit-scrollbar { display: none; }
  .exhibit { scroll-snap-align: start; }
  .viewer { padding: 16px; }
  .vprev, .vnext { top: auto; bottom: 14px; margin: 0; }
  .faq p { padding: 0 0 22px 56px; }
}
@media (prefers-reduced-motion: reduce) {
  html { scroll-behavior: auto; }
  *, *::before, *::after { animation: none !important; transition: none !important; }
  .js [data-reveal] { opacity: 1; transform: none; }
}
</style>
</head>
<body>
<div class="progress" aria-hidden="true"></div>
<a class="skip" href="#main">Skip to content</a>
<div class="ticker" id="top" aria-hidden="true"><div class="track">${[...ticker, ...ticker].map((t) => `<span>${t}</span>`).join("")}</div></div>
<header class="nav">
  <div class="wrap">
    <a class="brand" href="/" aria-label="Out of Office home"><img src="/apple-touch-icon.png" alt="" width="34" height="34">Out of Office</a>
    <nav class="nav-links" aria-label="Sections">
      <a href="#job"><b>01</b>The job</a>
      <a href="#floors"><b>02</b>Floors</a>
      <a href="#hazards"><b>03</b>Hazards</a>
      <a href="#plans"><b>04</b>Exhibits</a>
      <a href="#build"><b>05</b>How it's made</a>
      <a href="#faq"><b>06</b>FAQ</a>
    </nav>
    <a class="btn btn-ink" href="${PLAYTEST}">Request access</a>
  </div>
</header>

<main id="main">
  <section class="hero">
    <div class="wrap">
      <article class="mail" aria-label="Auto-reply">
        <dl class="mail-head">
          <dt>From</dt><dd>Out of Office &lt;${EMAIL}&gt;</dd>
          <dt>To</dt><dd>Everyone who asked</dd>
          <dt>Subject</dt><dd><span class="auto">Auto-reply</span>Re: Are you coming in today?</dd>
        </dl>
        <div class="mail-body">
          <h1 class="wordmark"><span class="w" style="--d:.1s">Out</span> <span class="w" style="--d:.2s">of</span> <span class="line2"><span class="w" style="--d:.35s">Office</span></span></h1>
          <p class="lead" data-reveal style="--d:.3s">Collect coffee. Survive the office. Reach the bed.</p>
          <p class="sub" data-reveal style="--d:.4s">A one-thumb physics puzzle about a very tired employee. Pull back, let go, and bank shots off desks, swivel chairs, and wet floors across 64 rooms to get home to bed.</p>
          <p class="sign" data-reveal style="--d:.5s">— Sent from my bed</p>
          <div class="actions" data-reveal style="--d:.55s">
            <a class="btn btn-ink" href="${PLAYTEST}">Join the playtest</a>
            <a class="btn btn-line" href="#plans">See the exhibits</a>
          </div>
        </div>
        <div class="stamp" aria-label="In development for iPhone and Android"><b>In development</b>iPhone + Android</div>
      </article>
      <div class="desk">
        <span class="ring" aria-hidden="true"></span>
        <div class="lanyard" aria-hidden="true">
          <div class="strap"></div>
          <div class="clip"></div>
          <div class="badge">
            <div class="hole"></div>
            <div class="co">Out of Office Inc.</div>
            <img src="/apple-touch-icon.png" alt="" width="74" height="74">
            <div class="who">The Player</div>
            <div class="role">Very tired employee</div>
            <div class="access">Access: floors 1–7</div>
            <div class="barcode"></div>
          </div>
        </div>
        <div class="holder" id="holder">${phone("orange", shot("home", "Out of Office home screen. A sleepy employee stands under the title, with a green Level 15 button.", "(min-width: 960px) 300px, 62vw", true))}</div>
        <div class="sticky" aria-hidden="true">3 coffees. Then bed.</div>
      </div>
    </div>
  </section>

  <section class="section" id="job" style="padding-top:24px">
    <div class="wrap">
      <article class="posting" data-reveal>
        <div class="ref"><span>Internal job posting</span><span>Ref. OOO-064</span><span>Location: your phone</span></div>
        <h2>Position: Very Tired Employee</h2>
        <p class="summary">You've sat through the meetings. You've survived the feedback. Now get from your desk to your bed, one room at a time, with one thumb.</p>
        <div class="cols">
          <div>
            <h3>Responsibilities</h3>
            <ol class="duties">
              <li><span class="n">01</span><div><strong>Pull back and let go</strong><span>Aim with one thumb. A short preview shows where the shot is headed.</span></div></li>
              <li><span class="n">02</span><div><strong>Collect the coffee</strong><span>Three cups in every room. All three, under par, is a perfect day.</span></div></li>
              <li><span class="n">03</span><div><strong>Reach the bed</strong><span>Bank off walls, desks, and coworkers. The boss's chair sends you back.</span></div></li>
            </ol>
          </div>
          <div>
            <h3>Requirements</h3>
            <ul class="boxes">
              <li>${tick}One thumb</li>
              <li>${tick}A decent sense of angles</li>
              <li>${tick}Low tolerance for meetings</li>
              <li>${tick}No experience necessary</li>
            </ul>
          </div>
          <div>
            <h3>Benefits</h3>
            <ul class="boxes">
              <li>${tick}A bed at the end of every room</li>
              <li>${tick}Coffee, three per room</li>
              <li>${tick}Outfits, hats, and stickers</li>
              <li>${tick}Fresh quests every midnight</li>
            </ul>
          </div>
        </div>
        <ul class="terms">
          <li><b data-count="64">64</b><span>Handmade rooms</span></li>
          <li><b data-count="7">7</b><span>Office floors</span></li>
          <li><b data-count="22">22</b><span>Mechanics</span></li>
          <li><b data-count="50" data-suffix="+">50+</b><span>Wardrobe items</span></li>
        </ul>
      </article>
    </div>
  </section>

  <section class="section" id="floors" style="padding-top:24px">
    <div class="wrap">
      <div class="head" data-reveal>
        <p class="kicker"><b>02</b>Building directory</p>
        <h2>Seven floors. <em>One way out.</em></h2>
        <p>Start in the open-plan office and work your way up to the executive floor. Each floor brings its own look, its own mechanics, and its own passive-aggressive signage. Press a floor.</p>
      </div>
      <div class="lobby" data-reveal>
        <div class="elevator">
          <div class="display" id="display" aria-live="polite"><span class="arrow" id="arr-up">▲</span><span id="floor-no">1</span><span class="arrow" id="arr-down">▼</span></div>
          <div class="plate">Out of Office Tower</div>
          <div class="buttons" role="group" aria-label="Choose a floor">
            ${floors.map((f, i) => `<button class="ebtn" type="button" data-floor="${i}" aria-pressed="${i === 0}" aria-label="Floor ${i + 1}: ${f.name}">${i + 1}</button>`).join("")}
          </div>
        </div>
        <div class="directory" id="directory">
          <div class="row1"><span>Floor 01</span><span>${floors[0].range}</span></div>
          <h3>${floors[0].name}</h3>
          <blockquote>${floors[0].sign}</blockquote>
          <ul>${floors[0].levels.map((l) => `<li><span>${l}</span><i>Room</i></li>`).join("")}</ul>
        </div>
      </div>
    </div>
  </section>

  <section class="section" id="hazards" style="padding-top:24px">
    <div class="wrap">
      <div class="head" data-reveal>
        <p class="kicker"><b>03</b>Known hazards</p>
        <h2>Incident reports</h2>
        <p>The office fights back. Every floor adds new mechanics, and every one of them has an opinion. These lines are straight from the game.</p>
      </div>
      <div class="cork" data-reveal>
        <ul class="notes">
          ${incidents.map((n, i) => `<li class="note" style="--n:${n.color};--r:${n.r}deg"><div class="no">Incident ${String(i + 1).padStart(3, "0")}</div><h3>${n.name}</h3><p>“${n.line}”</p></li>`).join("")}
        </ul>
      </div>
    </div>
  </section>

  <section class="section plans" id="plans">
    <div class="wrap">
      <div class="head" data-reveal>
        <p class="kicker"><b>04</b>Exhibits</p>
        <h2>Evidence, as requested.</h2>
        <p>Real screenshots from the current build, on iPhone 17 Pro Max. Select one to see it at full resolution.</p>
      </div>
      <ul class="exhibits">
        ${exhibits
          .map(
            (s, i) => `<li class="exhibit" data-reveal style="--d:${i * 0.1}s">
          <button type="button" data-full="/img/${s.name}-1290.jpg" data-alt="${s.alt}" aria-label="View ${s.label} full size">
            <span class="tape">${s.label}</span>
            ${phone(s.finish, shot(s.name, s.alt, "(min-width: 1200px) 350px, (min-width: 720px) 30vw, 74vw"))}
          </button>
          <h3>${s.title}</h3>
          <p>${s.line}</p>
          <span class="zoom">View full resolution →</span>
        </li>`
          )
          .join("")}
      </ul>
    </div>
  </section>

  <section class="section blueprint" id="build">
    <div class="wrap">
      <div class="head" data-reveal>
        <p class="kicker"><b>05</b>Spec sheet</p>
        <h2>How it's made.</h2>
        <p>Out of Office is built in Unity with one unusual rule: nothing off the shelf. Everything you see and hear is made in code, and every room is tested by a machine before a person ever plays it.</p>
      </div>
      <ul class="specs" data-reveal>
        ${specs.map((s, i) => `<li class="spec"><span class="idx">S-${String(i + 1).padStart(2, "0")}</span><b>${s.big}</b><span class="label">${s.label}</span><p>${s.text}</p></li>`).join("")}
      </ul>
      <div class="board" data-reveal>
        <h3>Shipping status</h3>
        <div class="lanes">
          ${status.map((l, i) => `<div class="lane"><h4>${l.col}<i>${l.items.length}</i></h4><ul>${l.items.map((t) => `<li style="--c:${["#5fd17a", "#ff8a1f", "#a8b8ff"][i]}">${t}</li>`).join("")}</ul></div>`).join("")}
        </div>
      </div>
    </div>
  </section>

  <section class="section" id="wardrobe">
    <div class="wrap dress">
      <div data-reveal>
        <p class="kicker"><b>06</b>Dress code</p>
        <h2>Dress for the job you want. <em>Sleeping.</em></h2>
        <p class="sub" style="font-size:18px">Unlock colors, hats, and face stickers with the coffee you collect, or earn them by finishing daily quests. Same physics, very different outfits.</p>
        <ul class="hangtags">
          ${tags.map((t) => `<li class="hangtag"><strong>${t.name}</strong><span>${t.from} → ${t.to}</span><em>new!</em></li>`).join("")}
        </ul>
        <ul class="rarity" aria-label="Rarities">
          <li><i style="--c:#8bc34a"></i>Common</li>
          <li><i style="--c:#3fa2ff"></i>Rare</li>
          <li><i style="--c:#b15cff"></i>Epic</li>
          <li><i style="--c:#ffb020"></i>Legendary</li>
        </ul>
      </div>
      <div class="clipboard" data-reveal style="--d:.1s">
        <div class="sheet">
          <h3>Today's to-do</h3>
          <p class="small">Three fresh quests every midnight, each with a reward.</p>
          <ul class="todo" id="todo"></ul>
          <div class="shift"><span>Shift resets in</span><b id="countdown">--:--:--</b></div>
        </div>
      </div>
    </div>
  </section>

  <section class="section" id="settings" style="padding-top:0">
    <div class="wrap split">
      <div data-reveal>${phone("silver", shot("pause", "Pause menu with switches for sound, music, and reduce motion.", "(min-width: 960px) 290px, 74vw"))}</div>
      <div data-reveal style="--d:.1s">
        <p class="kicker"><b>07</b>Wellbeing policy</p>
        <h2>HR-approved settings.</h2>
        <p class="sub" style="font-size:18px">Settings are one tap away from any level, and none of them change how a level plays. Go on, flip a few.</p>
        <ul class="form">
          <li><div><strong>Reduce motion</strong><span>Turns down camera shake and extra animation.</span></div><button class="toggle" type="button" role="switch" aria-checked="false" aria-label="Reduce motion"></button></li>
          <li><div><strong>Sound effects</strong><span>Every bounce, sip, and landing.</span></div><button class="toggle" type="button" role="switch" aria-checked="true" aria-label="Sound effects"></button></li>
          <li><div><strong>Music</strong><span>Keep it, or keep it quiet.</span></div><button class="toggle" type="button" role="switch" aria-checked="true" aria-label="Music"></button></li>
          <li><div><strong>Haptics</strong><span>Feel every hit on iPhone.</span></div><button class="toggle" type="button" role="switch" aria-checked="true" aria-label="Haptics"></button></li>
        </ul>
      </div>
    </div>
  </section>

  <section class="section" id="faq" style="padding-top:0">
    <div class="wrap">
      <div class="head" data-reveal>
        <p class="kicker"><b>08</b>Break room</p>
        <h2>Asked at the water cooler.</h2>
      </div>
      <div class="faq" data-reveal>
        ${faqs.map(([q, a], i) => `<details${i === 0 ? " open" : ""}><summary><i>Q${String(i + 1).padStart(2, "0")}</i>${q}</summary><p>${a}</p></details>`).join("")}
      </div>
    </div>
  </section>

  <section class="reception" id="contact">
    <div class="wrap">
      <div class="desk-card" data-reveal>
        <div>
          <p class="kicker" style="color:rgba(255,255,255,.65)"><b>09</b>Reception</p>
          <h2>Leave a message at the front desk.</h2>
          <p>Startup programs, publishers, press, and playtesters: we'd love to hear from you. We answer every email, even outside office hours.</p>
          <div class="actions">
            <a class="btn btn-ink" href="${PLAYTEST}">Join the playtest</a>
            <button class="btn btn-line" type="button" id="copy" data-email="${EMAIL}">Copy email</button>
          </div>
        </div>
        <div class="card-stage">
          <div class="bizcard" id="bizcard">
            <div class="top"><img src="/apple-touch-icon.png" alt="" width="46" height="46"><div><div class="name">Out of Office</div><div class="title">Games for very tired people</div></div></div>
            <span class="hours">back never!</span>
            <div class="lines"><div><span>e </span>${EMAIL}</div><div><span>w </span>gotobed.win</div><div><span>p </span>iPhone + Android</div></div>
          </div>
        </div>
      </div>
    </div>
  </section>
</main>

<footer>
  <div class="wrap">
    <p class="autoreply"><b>Automatic reply:</b> Thank you for your message. I am currently out of office and will not be returning. Out of Office is in development for iPhone and Android. For anything urgent, contact <a href="mailto:${EMAIL}">${EMAIL}</a>.</p>
    <div class="foot"><span>© 2026 Out of Office</span><span>Made with code, coffee, and very little sleep</span><a href="#top">Back to top ↑</a></div>
  </div>
</footer>

<dialog id="viewer" aria-label="Screenshot viewer">
  <div class="viewer"><img alt=""></div>
  <button class="vbtn vclose" type="button" aria-label="Close"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg></button>
  <button class="vbtn vprev" type="button" aria-label="Previous screenshot"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M15 6l-6 6 6 6"/></svg></button>
  <button class="vbtn vnext" type="button" aria-label="Next screenshot"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M9 6l6 6-6 6"/></svg></button>
  <span class="vcount"></span>
</dialog>
<div class="toast" id="toast" role="status" aria-live="polite"></div>

<script>
(() => {
  const FLOORS = ${JSON.stringify(floors)};
  const reduce = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const fine = matchMedia("(pointer: fine)").matches;
  const $ = (s, el) => (el || document).querySelector(s);
  const $$ = (s, el) => Array.from((el || document).querySelectorAll(s));
  const esc = (s) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);

  // Nav, progress, active section
  const nav = $(".nav"), bar = $(".progress"), links = $$(".nav-links a");
  const onScroll = () => {
    nav.classList.toggle("scrolled", scrollY > 40);
    const max = document.documentElement.scrollHeight - innerHeight;
    bar.style.transform = "scaleX(" + (max > 0 ? scrollY / max : 0) + ")";
    if (scrollY < innerHeight * 0.5) links.forEach((a) => a.classList.remove("active"));
  };
  addEventListener("scroll", onScroll, { passive: true });
  onScroll();
  const spy = new IntersectionObserver((entries) => {
    entries.forEach((e) => {
      if (e.isIntersecting) links.forEach((a) => a.classList.toggle("active", a.getAttribute("href") === "#" + e.target.id));
    });
  }, { rootMargin: "-45% 0px -50% 0px" });
  links.forEach((a) => { const t = $(a.getAttribute("href")); if (t) spy.observe(t); });

  // Reveal and counters
  const countUp = (el) => {
    const end = +el.dataset.count, suffix = el.dataset.suffix || "";
    if (reduce) { el.textContent = end + suffix; return; }
    const t0 = performance.now();
    const step = (t) => {
      const p = Math.min(1, (t - t0) / 1300);
      el.textContent = Math.round(end * (1 - Math.pow(1 - p, 3))) + suffix;
      if (p < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  };
  const reveal = new IntersectionObserver((entries) => {
    entries.forEach((e) => {
      if (!e.isIntersecting) return;
      e.target.classList.add("in");
      $$("[data-count]", e.target).forEach(countUp);
      $$(".todo li[data-done]", e.target).forEach((li, i) => setTimeout(() => li.classList.add("done"), 500 + i * 350));
      reveal.unobserve(e.target);
    });
  }, { threshold: 0.15 });
  $$("[data-reveal]").forEach((el) => reveal.observe(el));

  // Tilt helpers
  const tilt = (area, el, base, amount) => {
    if (reduce || !fine) return;
    area.addEventListener("pointermove", (e) => {
      const r = area.getBoundingClientRect();
      const x = (e.clientX - r.left) / r.width - 0.5, y = (e.clientY - r.top) / r.height - 0.5;
      el.style.transform = base + " rotateY(" + (x * amount).toFixed(2) + "deg) rotateX(" + (-y * amount * 0.8).toFixed(2) + "deg)";
    });
    area.addEventListener("pointerleave", () => { el.style.transform = ""; });
  };
  tilt($(".hero"), $("#holder"), "perspective(1200px)", 10);
  tilt($(".card-stage"), $("#bizcard"), "perspective(900px) rotate(-2deg)", 16);

  // Toast and copy
  const toast = $("#toast");
  let toastTimer;
  const say = (msg) => {
    toast.textContent = msg;
    toast.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove("show"), 2200);
  };
  $("#copy").addEventListener("click", (e) => {
    const email = e.currentTarget.dataset.email;
    (navigator.clipboard ? navigator.clipboard.writeText(email) : Promise.reject())
      .then(() => say("Copied " + email))
      .catch(() => say(email));
  });

  // Elevator
  const btns = $$(".ebtn"), display = $("#display"), no = $("#floor-no"), up = $("#arr-up"), down = $("#arr-down"), dir = $("#directory");
  let at = 0, target = 0, moving = null, auto = null, touched = false;
  const paint = (i) => {
    const f = FLOORS[i];
    dir.innerHTML = '<div class="row1 flip"><span>Floor ' + String(i + 1).padStart(2, "0") + "</span><span>" + f.range + '</span></div><h3 class="flip">' + esc(f.name) + '</h3><blockquote class="flip">' + esc(f.sign) + '</blockquote><ul class="flip">' + f.levels.map((l) => "<li><span>" + esc(l) + "</span><i>Room</i></li>").join("") + "</ul>";
  };
  const go = (i) => {
    target = i;
    btns.forEach((b, k) => b.setAttribute("aria-pressed", k === i));
    clearInterval(moving);
    if (reduce || at === target) { at = target; no.textContent = at + 1; paint(at); return; }
    moving = setInterval(() => {
      const d = target > at ? 1 : -1;
      at += d;
      no.textContent = at + 1;
      up.classList.toggle("on", d > 0);
      down.classList.toggle("on", d < 0);
      if (at === target) {
        clearInterval(moving);
        up.classList.remove("on");
        down.classList.remove("on");
        display.classList.remove("ding");
        void display.offsetWidth;
        display.classList.add("ding");
        paint(at);
      }
    }, 320);
  };
  btns.forEach((b) => b.addEventListener("click", () => { touched = true; clearInterval(auto); go(+b.dataset.floor); }));
  new IntersectionObserver((entries) => {
    entries.forEach((e) => {
      clearInterval(auto);
      if (e.isIntersecting && !touched && !reduce) auto = setInterval(() => go((target + 1) % FLOORS.length), 4200);
    });
  }, { threshold: 0.5 }).observe(dir);

  // Settings toggles
  $$(".toggle").forEach((t) => t.addEventListener("click", () => t.setAttribute("aria-checked", t.getAttribute("aria-checked") !== "true")));

  // Daily quests and countdown
  const POOL = [["Finish 3 rooms", 3], ["Grab 8 coffees", 8], ["Beat par in 2 rooms", 2], ["Score a hole-in-one", 1], ["Bounce off 10 chairs", 10], ["Take 25 shots", 25]];
  const now = new Date();
  let seed = now.getFullYear() * 10000 + (now.getMonth() + 1) * 100 + now.getDate();
  const rnd = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647; };
  const picks = POOL.slice().sort(() => rnd() - 0.5).slice(0, 3);
  $("#todo").innerHTML = picks.map((q, i) => {
    const done = i === 0 ? q[1] : Math.round(q[1] * [0, 0.6, 0.3][i]);
    return "<li" + (done === q[1] ? " data-done" : "") + '><span class="box">' + ${JSON.stringify(tick)} + '</span><span class="q">' + q[0] + "</span><em>" + done + "/" + q[1] + "</em></li>";
  }).join("");
  const cd = $("#countdown"), pad = (n) => String(n).padStart(2, "0");
  const tickClock = () => {
    const t = new Date(), m = new Date(t);
    m.setHours(24, 0, 0, 0);
    const s = Math.max(0, Math.floor((m - t) / 1000));
    cd.textContent = pad(Math.floor(s / 3600)) + ":" + pad(Math.floor((s % 3600) / 60)) + ":" + pad(s % 60);
  };
  tickClock();
  setInterval(tickClock, 1000);

  // Lightbox
  const dialog = $("#viewer"), shots = $$("[data-full]");
  if (dialog.showModal) {
    const img = $("img", dialog), count = $(".vcount", dialog);
    let cur = 0;
    const show = (i) => {
      cur = (i + shots.length) % shots.length;
      img.src = shots[cur].dataset.full;
      img.alt = shots[cur].dataset.alt;
      img.style.animation = "none";
      void img.offsetWidth;
      img.style.animation = "";
      count.textContent = "Exhibit " + "ABC"[cur] + " · " + (cur + 1) + " / " + shots.length;
    };
    shots.forEach((b, i) => b.addEventListener("click", () => { show(i); dialog.showModal(); }));
    $(".vclose", dialog).addEventListener("click", () => dialog.close());
    $(".vprev", dialog).addEventListener("click", () => show(cur - 1));
    $(".vnext", dialog).addEventListener("click", () => show(cur + 1));
    dialog.addEventListener("click", (e) => { if (e.target.classList.contains("viewer")) dialog.close(); });
    dialog.addEventListener("keydown", (e) => {
      if (e.key === "ArrowRight") show(cur + 1);
      if (e.key === "ArrowLeft") show(cur - 1);
    });
    let sx = null;
    dialog.addEventListener("touchstart", (e) => { sx = e.touches[0].clientX; }, { passive: true });
    dialog.addEventListener("touchend", (e) => {
      if (sx === null) return;
      const dx = e.changedTouches[0].clientX - sx;
      if (Math.abs(dx) > 50) show(cur + (dx < 0 ? 1 : -1));
      sx = null;
    });
  }
})();
</script>
</body>
</html>`;

// Legal and support pages for the store listings (OUT-54). They describe what the game does today: progress stays on
// the phone, one time check goes to Google, rewarded ads come from Google AdMob, and purchases go through the stores.
const UPDATED = "17 October 2026";
const legal = (path, title, body) => `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>${title} · Out of Office</title>
<meta name="theme-color" content="#24104a">
<link rel="canonical" href="https://gotobed.win${path}">
<link rel="icon" href="/favicon.png" type="image/png">
<style>
:root { color-scheme: light; --ink: #1d1330; --muted: #5f566f; --paper: #f3ede2; --sheet: #fffdf8; --purple: #3a1a6e; --orange: #ff8a1f; }
* { box-sizing: border-box; }
body { margin: 0; background: var(--paper); color: var(--ink); font: 17px/1.6 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
main { max-width: 720px; margin: 0 auto; padding: 32px 20px 64px; }
.back { display: inline-block; margin-bottom: 24px; color: var(--purple); font-weight: 700; text-decoration: none; }
article { background: var(--sheet); border-radius: 18px; padding: 28px 24px; box-shadow: 0 2px 0 rgba(29,19,48,.08); }
h1 { margin: 0 0 4px; color: var(--purple); font-size: 32px; line-height: 1.15; }
.updated { margin: 0 0 24px; color: var(--muted); font-size: 14px; }
h2 { margin: 28px 0 8px; font-size: 20px; }
a { color: var(--purple); }
li { margin: 6px 0; }
</style>
</head>
<body>
<main>
<a class="back" href="/">&larr; Out of Office</a>
<article>
<h1>${title}</h1>
<p class="updated">Last updated ${UPDATED}</p>
${body}
</article>
</main>
</body>
</html>`;

const pages = {
  "/privacy": legal("/privacy", "Privacy Policy", `
<p>Out of Office is a puzzle game for iPhone and Android. This policy explains what information the game uses and why. We keep it short because the game collects very little.</p>
<h2>What stays on your phone</h2>
<ul>
<li>Your progress, settings, items, gems and the energy timer are saved on your device.</li>
<li>The game makes a random install number, used only to keep A/B tests and statistics consistent on your device. It is not linked to your name, email or contacts.</li>
<li>Gameplay events (for example a room finished or a booster used) are recorded on your device to help us improve the game. The current version does not send them to us.</li>
</ul>
<h2>What leaves your phone</h2>
<ul>
<li><b>A time check.</b> When the game starts, it asks a Google web server for the current date and time, so daily rewards can't be unlocked by changing the phone's clock. Like any web request, this shares your IP address with Google. No game data is sent.</li>
<li><b>Ads.</b> The game offers optional rewarded videos from Google AdMob. To show and measure ads, Google may collect your device's advertising ID, IP address and ad interactions, and may personalise ads where you allow it. In the EEA, the UK and Switzerland, the game asks for your consent first using Google's consent message, and you can change your choice later. See <a href="https://policies.google.com/technologies/partner-sites">how Google uses information from apps that use its services</a>.</li>
<li><b>Purchases.</b> In-app purchases are handled by Google Play or the App Store. We never see your payment details; the store tells the game only what you bought.</li>
</ul>
<h2>What we don't do</h2>
<ul>
<li>We don't ask for your name, email, contacts, location, photos or microphone.</li>
<li>We don't sell your information.</li>
</ul>
<h2>Children</h2>
<p>Out of Office is not directed at children under 13, and we don't knowingly collect personal information from them.</p>
<h2>Your choices</h2>
<p>You can reset your advertising ID or opt out of personalised ads in your phone's settings. Uninstalling the game deletes everything it saved on your device.</p>
<h2>Changes and contact</h2>
<p>If the game starts using information in a new way, we'll update this page first. Questions? Write to <a href="mailto:${EMAIL}">${EMAIL}</a>.</p>`),

  "/terms": legal("/terms", "Terms of Use", `
<p>By playing Out of Office you agree to these terms. If you don't agree, please don't play.</p>
<h2>Your licence</h2>
<p>We give you a personal, non-transferable licence to play the game on your own devices. The game, its art and its code stay ours.</p>
<h2>Virtual items</h2>
<ul>
<li>Gems, clips, boosters, energy, staff and wardrobe items are part of the game. They have no cash value and can't be exchanged for money.</li>
<li>Purchases are processed by Google Play or the App Store, and their refund rules apply.</li>
<li>Chest contents are random. The odds for every chest are shown in the game before you open it.</li>
</ul>
<h2>Fair play</h2>
<p>Please don't cheat, modify the game or interfere with ads or purchases. We may reset progress gained that way.</p>
<h2>No warranty</h2>
<p>The game is provided as it is. We work hard to keep it running well, but we can't promise it will always be available or free of bugs. To the extent the law allows, we aren't liable for indirect losses from using the game.</p>
<h2>Changes and contact</h2>
<p>We may update these terms; the date above shows when. Questions? Write to <a href="mailto:${EMAIL}">${EMAIL}</a>.</p>`),

  "/support": legal("/support", "Support", `
<p>Stuck, found a bug, or a purchase didn't arrive? We're happy to help.</p>
<h2>Contact</h2>
<p>Email <a href="mailto:${EMAIL}">${EMAIL}</a>. Please include your phone model and, for a purchase, the order number from your store receipt.</p>
<h2>Common questions</h2>
<ul>
<li><b>I bought gems but didn't get them.</b> Restart the game while online. If they still don't arrive, email us with the order number.</li>
<li><b>Can I replay the tutorial?</b> Yes: Settings, then Tutorial.</li>
<li><b>Will I lose my progress if I change phones?</b> Progress is saved on your phone today. Cloud saves are on the way.</li>
<li><b>How do I stop personalised ads?</b> See the <a href="/privacy">privacy policy</a>.</li>
</ul>`),
};

const security = {
  "x-content-type-options": "nosniff",
  "referrer-policy": "strict-origin-when-cross-origin",
  "permissions-policy": "camera=(), microphone=(), geolocation=()",
};

const assetPaths = {
  "/favicon.png": "favicon-64.png",
  "/favicon.ico": "favicon-64.png",
  "/apple-touch-icon.png": "apple-touch-icon.png",
  "/icon-512.png": "icon-512.png",
};

const legacy = {
  "/icon.jpg": "/apple-touch-icon.png",
  "/shots/home.jpg": "/img/home-720.jpg",
  "/shots/badge.jpg": "/img/badge-720.jpg",
  "/shots/bridge.jpg": "/img/bridge-720.jpg",
  "/shots/pause.jpg": "/img/pause-720.jpg",
};

const text = (body, type) =>
  new Response(body, {
    headers: { ...security, "content-type": type, "cache-control": "public, max-age=3600" },
  });

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.hostname === "www.gotobed.win") {
      url.hostname = "gotobed.win";
      url.protocol = "https:";
      return Response.redirect(url.toString(), 301);
    }

    const key = assetPaths[url.pathname] ?? url.pathname.match(/^\/img\/([a-z]+-(?:720|1290)\.jpg)$/)?.[1];
    if (key) {
      const { value, metadata } = await env.ASSETS.getWithMetadata(key, { type: "stream", cacheTtl: 86400 });
      if (!value) return new Response("Not found", { status: 404, headers: security });
      return new Response(value, {
        headers: {
          ...security,
          "content-type": metadata?.type ?? "application/octet-stream",
          "cache-control": "public, max-age=604800, stale-while-revalidate=86400",
        },
      });
    }

    if (legacy[url.pathname]) return Response.redirect(`https://gotobed.win${legacy[url.pathname]}`, 301);
    if (url.pathname === "/robots.txt") return text("User-agent: *\nAllow: /\nSitemap: https://gotobed.win/sitemap.xml\n", "text/plain; charset=utf-8");
    if (url.pathname === "/sitemap.xml") {
      return text(
        '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc>https://gotobed.win/</loc></url><url><loc>https://gotobed.win/privacy</loc></url><url><loc>https://gotobed.win/terms</loc></url><url><loc>https://gotobed.win/support</loc></url></urlset>\n',
        "application/xml; charset=utf-8"
      );
    }
    if (pages[url.pathname]) {
      return new Response(pages[url.pathname], {
        headers: { ...security, "content-type": "text/html; charset=utf-8", "cache-control": "public, max-age=3600" },
      });
    }
    if (url.pathname !== "/") return Response.redirect("https://gotobed.win/", 302);

    return new Response(html, {
      headers: {
        ...security,
        "content-type": "text/html; charset=utf-8",
        "cache-control": "public, max-age=300",
      },
    });
  },
};
