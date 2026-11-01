# Harbor and Boat L1–L5 sprites

Created with built-in image_gen. Original `PierBoat.png` is retained unchanged as the style and orientation reference. Each asset is a separate 1536×1024 RGBA PNG with genuine transparency, imported as single full-rect sprites (Harbor at 256 PPU; Boat uses the calibrated per-level PPU listed below), no mipmaps, uncompressed default texture settings.

Harbor: timber L1; maintained deck L2; darker timber, metal rails and a compact entrance canopy L3; refined dark deck and continuous lighting L4; pearl-white, light decking, anthracite canopy and champagne-gold details L5. The intended deck footprint is fixed; generated geometry is visually constrained, not pixel-identical.

Boat: open motorboat L1; windshield and canvas bimini L2; enclosed cabin L3; streamlined sport cruiser and swim platform L4; pearl-white luxury cruiser with gold trim and bow sunpads L5. Boat heading remains bow toward lower-right and stern toward upper-left. No baked water or pier.

Import pivots currently use the image center. Final placement anchors and scale alignment belong to the later prefab pass. No prefabs or scenes were changed.

## Prompts

### harbor_l1

Use case: precise-object-edit. Asset type: standalone transparent isometric resort game sprite HARBOR LEVEL 1. Reference image is existing combined PierBoat; extract/redraw ONLY its wooden pier, completely REMOVE boat, boat ropes, wakes and all water. Preserve original pier long narrow rectangular footprint, exact diagonal orientation from bottom-left to top-right, fixed camera and detailed polished stylized 3D Mediterranean resort rendering. Simple warm timber deck and thick timber piles, simple rope railings, modest warm lanterns and one lifebuoy. Retain same deck footprint and all pier corners as reference, no extra platforms or width extensions. Boat will be a SEPARATE asset. Full pier visible with generous transparent margins on 1536x1024 landscape canvas. True clean transparent background, no black/white backdrop, no diffuse glow, ground shadows, water, shoreline, text, people, logos or watermark. ONE sprite, not a sheet.

### harbor_l2

Use case: precise-object-edit. HARBOR LEVEL 2. Image 1 is L1 EDIT TARGET and immutable footprint blueprint. Change ONLY small finish details: slightly more muted warm timber, well-maintained straight deck boards, improved metal mooring cleats, neat ropes and subtly upgraded small warm deck lights. Retain L1 lantern positions, lifebuoy and all pile positions. Do NOT add a roof or extra platform. EXACT same long narrow rectangular deck perimeter and every deck corner/pile location in image, identical canvas1536x1024, camera angle, size and placement. Keep same detailed stylized resort game art. One isolated pier only, NO BOAT, water, shore or external mooring rope. Genuine transparent background, no glow/haze/shadow outside silhouette, clean margins, no people/text/logos. Subtle L2 upgrade.

### harbor_l3

Use case: precise-object-edit. HARBOR LEVEL3. Image1 L1 pier is EXACT footprint EDIT TARGET. Modernize SAME pier: recolor all timber deck and support piles medium DARK TAUPE BROWN #8C6552, grey metal post caps and cleats, refined slim grey metal railing replacing some rope spans, integrated warm low deck lighting. ADD ONE SMALL LOW ANTHRACITE canopy over far upper-right entrance section only, flat/slightly sloping compact roof on four slender grey posts, leaving at least two-thirds of pier uncovered. Roof and support feet entirely within EXISTING deck outline, no platform extensions. Keep all original deck corners, perimeter, pile positions, lifebuoy, identical long narrow diagonal bottom-left to upper-right orientation, same scale and1536x1024 placement. Detailed stylized 3D resort game art; darker modern L3 styling consistent with greige resort walls and brown roofs. ONE pier NO BOAT, no water, shore, backdrop, halo or outer shadows; genuinely transparent background, all corners/roof visible, no people/text/logo.

### harbor_l4

Use case: precise-object-edit. HARBOR LEVEL4. Image1 L3 is exact EDIT TARGET, keep SAME narrow rectangular deck footprint, all corners and piles, canopy roof size/orientation/position, image1536x1024 camera/scale/placement. Small premium refinement ONLY: darker rich smoked timber deck #62534C, warm grey structural post finishes #C5C0B6, refined dark brushed metal balustrade with single horizontal timber handrail, replace small deck spotlights with neat continuous warm LED strip beneath both deck side edge beams and canopy inner edge. Preserve modest flat dark canopy over upper-right entrance ONLY, lifebuoy and uncluttered deck. Do NOT add more roofs/platforms/buildings. Polished detailed stylized 3D Mediterranean resort game sprite. One isolated pier, no boat, water, shore, people, text/logo. Genuine transparency outside silhouette, no haze or shadows, full object visible.

### harbor_l5

Use case: precise-object-edit. HARBOR LEVEL5 luxury resort pier. Image1 L4 is EXACT EDIT TARGET. Preserve architecture: same SMALL FLAT CANOPY over far upper-right entrance only, same narrow rectangular deck outline, exact corner/pile/lantern positions, lifebuoy, camera/size/placement1536x1024. Change materials to flagship L5 palette: light pale ivory teak/composite decking, PEARL-WHITE #F0EEE7 posts/pile cladding and fascia, ANTHRACITE #292F35 canopy roof, thin CHAMPAGNE-GOLD frames/caps/cleats, crystal-clear glass infill between slim gold rail posts and a restrained dark handrail. Refined warm integrated lighting along deck edges and canopy. Keep white bright and clear, not yellow/beige/brown. Deck is still a working narrow pier, NOT a pavilion/hotel. No new platforms, no widening/lengthening, no extra roof, no furniture or planters. Polished detailed stylized 3D Mediterranean resort game asset matching premium white L5 buildings. One full isolated pier only, NO BOAT, water, shoreline, backdrop, glow/haze/shadow outside silhouette. Genuine transparent background, no people/text/logos.

### boat_l1

Use case: precise-object-edit. Asset type: BOAT LEVEL 1 standalone transparent isometric resort game sprite. Image 1 existing combined PierBoat is orientation and rendering STYLE reference. Create ONLY one small affordable OPEN MOTORBOAT, no enclosed cabin, modest cream-white hull, dark blue thin waterline stripe, varnished wood gunwales and warm orange simple seat cushions, a small steering console and outboard motor at stern. Same diagonal boat heading as reference: pointed BOW toward bottom-right, STERN toward upper-left, showing deck/top and near hull side from same elevated isometric view. The boat should be centered and fully visible, long-axis roughly upper-left to lower-right, occupy about x180..1360 y230..830 on 1536x1024 landscape canvas. Clean detailed stylized 3D resort game art, soft sunlight upper-left, restrained warm wood. Only the boat: NO pier, harbor, mooring ropes to external points, water, ripples, wake, reflections, foam, ground, props or cast shadow. GENUINE transparent empty background with clean silhouette, no haze or glow. No people, text, logos, watermark. ONE sprite, not a sheet.

### boat_l2

Use case: precise-object-edit. BOAT LEVEL 2, one standalone transparent isometric resort game sprite. Image1 L1 boat is EDIT TARGET: preserve its hull outline, exact heading BOW bottom-right STERN upper-left, camera angle, size and placement on1536x1024 canvas. Upgrade to a comfortable open motorboat with a full curved clear windshield around helm, thicker tan/caramel cushioned seats and ONE small warm taupe canvas bimini canopy on thin steel supports over rear seating. Modernize polished cleats and rails, muted ivory hull, restrained dark-blue waterline stripe and timber deck. Clear visible L2 improvement but NO enclosed cabin yet. Maintain detailed polished stylized 3D Mediterranean resort art, same lighting. Only boat; NO pier, water, wake, foam, cast shadow, mooring ropes to outside, people, text or logos. Genuine transparent background, no halo/glow/haze, entire canopy/motor/hull within canvas with clean margin.

### boat_l3

Use case: precise-object-edit. BOAT LEVEL3. Image1 L1 is silhouette/orientation blueprint, upgrade same boat to a compact CABIN CRUISER. Replace open helm with low enclosed cabin and broad dark panoramic windows, ANTHRACITE low solid roof with tiny radar dome, grey-white hull and greige/taupe timber deck trim, restrained blue-grey waterline stripe. Keep small open rear cockpit and stainless bow rails. Obvious new enclosed cabin silhouette distinguishes L3 from open L2. Keep exact heading BOW bottom-right STERN upper-left and same elevated isometric camera, overall hull footprint/width/length and placement within1536x1024 canvas; can lower overall framing slightly ONLY if needed for 40px clear canopy margin, do not enlarge boat. Match detailed polished stylized 3D Mediterranean resort game art. Entire boat only, NO pier, water,wake,foam,ground,external ropes,cast shadow,people,text or logos. GENUINE transparent background, no haze/halo around hull. One complete sprite.

### boat_l4

Use case: precise-object-edit. BOAT LEVEL4 sport cabin cruiser. Image1 L3 is EDIT TARGET, preserve same hull size, footprint and exact heading BOW bottom-right STERN upper-left, camera,1536x1024 placement. Modernize L3 cabin into sleek streamlined SPORT CRUISER with broad wraparound dark panoramic glazing, low flowing dark slate #45474A hardtop extended slightly over rear seating, warm grey hull side #C5C0B6 with white upper body, refined smoked teak deck and cream premium upholstery. Add a small integrated swim/boarding platform at stern WITHIN original motor silhouette space; use concealed/inboard propulsion instead of bulky exposed outboard. Keep compact single deck, NO flybridge yet. Readable L4 silhouette, entire vessel within margins. Detailed stylized 3D Mediterranean resort game art, same soft sunlight. ONLY boat, NO pier/water/wake/foam/ground/cast shadow/external mooring lines, no people,text or logos. Genuine transparent background, no haze/glow.


## Lighting and passenger-capacity revision

Generated with built-in image_gen. Harbor L1/L2 now use small plain cylindrical dock lights in dark iron, with no ornate brass lanterns. Harbor L3–L5 and all harbor deck footprints remain unchanged in this revision.

Boat L1–L5 have progressively more usable passenger seating and cockpit area. Higher levels modestly lengthen the hull instead of merely scaling furniture. Growth is permitted for boats only. No gameplay capacity values have been changed.

Generated framing varies; texture import PPU is calibrated using the alpha >127 silhouette width so the world-space visible widths are exactly 100%, 105%, 110%, 115%, and 120% relative to L1, at equal Transform scale. The comparison preview uses this same world-space scale. Image-center pivots remain provisional until prefab placement. All PNGs remain original generated RGBA, without resampling.

| Level | Pixels per unit | Relative visible width |
| --- | ---: | ---: |
| L1 | 256.0 | 100% |
| L2 | 294.128934 | 105% |
| L3 | 281.140645 | 110% |
| L4 | 275.298223 | 115% |
| L5 | 266.448266 | 120% |

### Revision prompts

#### harbor1

Use case: precise-object-edit. HARBOR L1 lighting revision. Image1 is EDIT TARGET. Change ONLY the FOUR ornate brass lanterns: replace each with a small plain utilitarian weatherproof dock light, squat simple cylindrical frosted-white diffuser, dark iron base and flat cap, no decorative gold/finials/pyramidal roof, modest warm light. These are cheap practical pier lights, approximately HALF the old lantern height. Keep their original locations atop timber posts. EXACTLY preserve every deck corner, timber color/material, railing ropes, cleats, lifebuoy, piles and all other pixels/composition as far as possible. SAME footprint, scale, placement,1536x1024 canvas and diagonal camera. ONE pier only, no boat or water. Genuine transparent background, no external glow/haze/shadow, no people/text/logo.

#### harbor2

Use case: precise-object-edit. HARBOR L2 lighting revision. Image1 L2 is EDIT TARGET, Image2 revised L1 is ONLY lamp design reference. Replace ALL FOUR ornate brass lanterns in image1 with the SAME small plain cylindrical practical dock lights as image2: frosted-white cylinder, dark iron flat cap/base, no gold or ornament, half previous lantern height, modest warm light. Keep L2 deck, original exact footprint/corners/piles, material, ropes, lifebuoy and framing unchanged. Same1536x1024 canvas/camera. Do not add any canopy or extra platform. ONE isolated pier only, no boat/water/shore. Genuine transparency no exterior haze/shadow/glow, no text/people/logos.

#### boat1

Use case: precise-object-edit. BOAT L1 capacity progression revision. Image1 L1 open motorboat is EDIT TARGET and style/orientation reference. Keep same cream hull, orange seat cushions, blue stripe, timber deck, open steering console, single outboard motor. Affordable SMALL open passenger launch: four passenger seating positions in simple bench arrangement, no roof or cabin, no people. Important shared scale for five-level set: redraw the WHOLE boat SMALLER, centered on1536x1024 canvas with visible silhouette spanning approximately x220..1315 (1095px width). Boat center at(768,540), generous empty margins, not zoomed fullframe. This is base size100%, later levels will grow only5% perlevel up to120%. Keep bow pointing bottom-right and stern upper-left, same elevated isometric angle and hull proportions, detailed stylized3D resort game art. TRUE transparent background, no pier/water/wake/foam/castshadow/backdrop/halo, no external rope/text/logo. ONE whole boat, fully visible.

#### boat2

Use case: precise-object-edit. BOAT L2 capacity revision. Image1 is L2 edit target, keep its style, cream/blue/timber/orange palette, curved windshield, single small canvas bimini canopy and open boat design. Extend hull and usable passenger cockpit MODESTLY along length (about5% relative to L1), with noticeably longer rear bench and forward U-shaped seating for a few more passengers, keep bench cushion sizes human-scaled, do not simply inflate all furniture. Preserve same bow-bottom-right/stern-upper-left heading and isometric angle. Whole boat centered on1536x1024 at(768,540), approx x140..1400 width1260px, ample margins, canopy entirely inside image. One compact motor launch not a huge vessel, NO cabin yet. Detailed stylized3D resort art. True clean transparent background; no people, pier, water, wake, foam, castshadow, haze, text or logos.

#### boat3

Use case: precise-object-edit. BOAT L3 passenger capacity revision. Image1 L3 is EDIT TARGET, retain compact enclosed cabin, dark anthracite roof and panoramic windows, greige deck, greywhite hull and existing stylistic progression. Modestly LENGTHEN the hull and usable REAR passenger cockpit by about10% relative to small L1. Rear cockpit needs TWO discernible facing upholstered bench sections with access aisle, more seating than an open small motorboat. Add visible interior passenger bench through side cabin glass. Increase usable space not cushion scale, keep cabin compact so it does not consume all passenger space. Same elevated isometric view, pointed BOW bottom-right STERN upper-left. Boat centered(768,540) on1536x1024 with visible width approx1320px and generous transparent margin; fully visible outboard/rails. Detailed stylized3D resort game rendering. Genuine transparent background, no pier,water,wake,foam,shadow,haze,people,text/logo. ONE vessel.

#### boat4

Use case: precise-object-edit. BOAT L4 passenger capacity revision. Image1 L4 is EDIT TARGET. Retain sleek low dark hardtop, greywhite hull, panoramic glazing, cream upholstery and rear swim platform, Mediterranean stylized3D resort rendering. Modestly extend usable hull/cockpit along length to support about15% larger hull than L1, not a huge yacht. Create TWO long facing side benches in open stern cockpit plus a transverse rear bench, visible central walkway to compact cabin. Show another passenger bench inside glazing. Passenger cushions stay normal size; increase quantity and deck space, cabin takes only central third. Same bow-bottom-right/stern-upper-left elevated isometric view,1536x1024 canvas, centered(768,540) silhouette approx1380px wide, fully visible with40px+ margins. No flybridge, no extra tall storey. Isolated whole vessel only, genuine transparent background, no water/wake/foam/pier/ground/shadow/haze/people/text/logo.

#### boat5

Use case: precise-object-edit. BOAT L5 passenger capacity revision. Image1 L5 luxury cruiser is EDIT TARGET, retain pearl-WHITE hull/exterior, anthracite low hardtop, panoramic dark glass and restrained champagne gold, pale teak deck. Modestly lengthen hull and enlarge usable passenger deck to approximately20% larger than small L1, NOT a giant superyacht. Cabin compact in middle third, clearly visible open aft cockpit with THREE connected long ivory passenger benches in U-shaped arrangement and access walkway, more seating space than L4. At bow replace oversized sunpad with two comfortable forward-facing passenger bench sections and central access. More passengers rather than decorative sunbeds. Keep coherent clean yacht silhouette single low deck, no giant upper floor. Same exact heading bow bottom-right/stern upper-left, elevated isometric camera. Centered boat on1536x1024, around1400px wide, at least40px margins on all sides, fully visible rails/bow/stern. Polished detailed stylized3D resort rendering. Genuine transparent background no haze, water,wake,foam,pier,ground,shadow,people,text/logo. ONE complete boat.



## L1 rustic simplification

Built-in image_gen edit. L1 now has matte raw timber, bare wooden posts and piles without silver caps or cuffs, simpler rope connections and two plain dock lights. L2 retains varnished boards, metal reinforcement and four lights. Original footprint and framing were constrained through image references; no import pivot, prefab or scene changes.

Prompt:

Use case: precise-object-edit. Asset: HARBOR LEVEL1 simplification, transparent isometric resort game sprite. Image1 current HarborL1 is EDIT TARGET and immutable footprint. Image2 HarborL2 is UPGRADE CONTINUITY reference, must remain recognizably the more polished upgrade of new L1. Image3 original PierBoat is rustic timber character reference ONLY, do not include its boat or ornate lamps. Make image1 visibly BASIC and modest: replace glossy orange varnished wood with slightly weathered matte warm natural honey-brown rough-sawn boards, visible simple grain, restrained uneven board joins but sturdy intact deck. Remove ALL shiny silver caps from railing posts, leaving plain flat wooden tops. Remove ALL large metal cuffs/plates at pile feet, leaving bare timber piles with only discreet iron nails/bolts at joints. Simplify bulky repeated decorative rim blocks into plain timber edge beams. Retain single rope railing on both long sides with simple knots, no polished silver collars. Remove the two middle lamps entirely, leave exactly TWO small plain dark iron cylindrical dock lights at opposite ends of far-side railing, modest warm illumination. Keep lifebuoy at exactly original location and simple useful mooring cleats. No canopy, furniture or added platform. CRITICAL retain EXACT deck rectangular perimeter, all four deck corner pixel coordinates, all main pile positions/lengths,1536x1024 image size, original camera/scale/placement and anchor; don't shrink/narrow/shorten/move the pier. Detailed polished stylized3D resort rendering, humble earlygame construction, not ruined/broken. L2 clearly adds varnish, metal caps/cuffs and more lights to this same pier. One isolated physical pier with full clean genuine transparency, all empty space alpha0 including between piles; NO surrounding haze/shadow/glow, water, boat, people,text/logo. Entire object fully visible.
