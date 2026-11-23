using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Nightshift
{
    public sealed class GameHud
    {
        static readonly Color Accent = new Color(.73f,.80f,.60f);
        static readonly Color Type = new Color(.93f,.96f,1);
        static readonly Color Muted = new Color(.71f,.79f,.87f);
        static readonly Color Tile = new Color(.032f,.056f,.083f,.85f);
        readonly GameManager game;
        readonly Font font;
        readonly Canvas canvas;
        readonly RectTransform screen;
        readonly RectTransform menu, paused, ended, drive, replay;
        readonly GaragePanel garage;
        readonly Text eventTitle, eventDetail, draftStatus, selectedCar;
        readonly Button replayAction, replayExit;
        readonly Text score, best, km, speed, route, life, fuelNumber, nitroNumber, nitroLabel, gear, toast, finalReason, finalResult, mute, courseMode;
        readonly Image fuelFill, nitroFill, impact;
        readonly Image[] tach = new Image[18];
        readonly Button mainAction, resumeAction, restartAction, pauseButton;
        readonly string[] zones = { "ALPINE PASS", "GLACIER TUNNEL", "RAVEN BRIDGE" };
        readonly EventSystem input;
        GameState previous;
        bool refreshed;

        public GameHud(GameManager game)
        {
            this.game = game;
            garage = new GaragePanel(game);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Nightshift HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(game.transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = canvasObject.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1600, 900);
            scale.matchWidthOrHeight = .37f;
            screen = (RectTransform)canvas.transform;
            var events = new GameObject("New Input System UI", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(game.transform, false);
            input = events.GetComponent<EventSystem>();
            var inputModule = events.GetComponent<InputSystemUIInputModule>();
            if (inputModule.actionsAsset == null) inputModule.AssignDefaultActions();

            impact = ImageArea("Collision tint", screen, new Vector2(.5f,.5f), Vector2.zero, new Vector2(1800,1050),
                Color.clear, false);
            impact.rectTransform.anchorMin = Vector2.zero; impact.rectTransform.anchorMax = Vector2.one;
            impact.rectTransform.offsetMin = impact.rectTransform.offsetMax = Vector2.zero;
            impact.raycastTarget = false;

            drive = Full("Driving HUD", screen);
            score = Metric(drive,"SCORE",new Vector2(46,-124));
            best = Metric(drive,"BEST",new Vector2(171,-124));
            km = Metric(drive,"KM",new Vector2(296,-124));
            route = Label(drive, "ALPINE PASS", new Vector2(1,1),new Vector2(-164,-139),new Vector2(220,26),18,Type, TextAnchor.MiddleRight, FontStyle.Bold);
            Label(drive, "ROUTE", new Vector2(1,1),new Vector2(-164,-112),new Vector2(220,26),13,Muted, TextAnchor.MiddleRight);
            life = Label(drive, "♥  ♥  ♥", new Vector2(0,0),new Vector2(171,143),new Vector2(250,38),30,new Color(.89f,.38f,.45f),TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(drive, "FUEL",new Vector2(0,0),new Vector2(89,105),new Vector2(86,25),15,Muted,TextAnchor.MiddleLeft);
            fuelNumber = Label(drive,"100%",new Vector2(0,0),new Vector2(272,105),new Vector2(52,25),15,Type,TextAnchor.MiddleRight);
            ImageArea("Fuel track",drive,new Vector2(0,0),new Vector2(172,87),new Vector2(252,8),new Color(.30f,.37f,.46f,.76f),false);
            fuelFill = ImageArea("Fuel remaining",drive,new Vector2(0,0),new Vector2(46,87),new Vector2(252,8),Accent,false);
            fuelFill.rectTransform.pivot = new Vector2(0,.5f); fuelFill.rectTransform.anchoredPosition = new Vector2(46,87);

            nitroLabel = Label(drive,"NITRO · W",new Vector2(0,0),new Vector2(138,62),new Vector2(184,25),15,Muted,TextAnchor.MiddleLeft);
            nitroNumber = Label(drive,"100%",new Vector2(0,0),new Vector2(272,62),new Vector2(52,25),15,Type,TextAnchor.MiddleRight);
            ImageArea("Nitro track",drive,new Vector2(0,0),new Vector2(172,44),new Vector2(252,8),new Color(.30f,.37f,.46f,.76f),false);
            nitroFill = ImageArea("Nitro remaining",drive,new Vector2(0,0),new Vector2(46,44),new Vector2(252,8),new Color(.63f,.71f,.79f),false);
            nitroFill.rectTransform.pivot = new Vector2(0,.5f); nitroFill.rectTransform.anchoredPosition = new Vector2(46,44);

            Vector2 bottomRight = new Vector2(1,0);
            ImageArea("Instrument panel",drive,bottomRight,new Vector2(-206,123),new Vector2(318,180),new Color(.042f,.062f,.09f,.96f),false);
            ImageArea("Instrument lip",drive,bottomRight,new Vector2(-206,34),new Vector2(318,2),new Color(.47f,.54f,.62f),false);
            Label(drive,"RPM",bottomRight,new Vector2(-306,190),new Vector2(82,17),12,Muted,TextAnchor.MiddleLeft,FontStyle.Bold);
            Label(drive,"×1000",bottomRight,new Vector2(-95,190),new Vector2(60,17),12,Muted,TextAnchor.MiddleRight);
            for (int i = 0; i < tach.Length; i++)
            {
                tach[i] = ImageArea("Tachometer segment",drive,bottomRight,new Vector2(-341 + i * 16,171),new Vector2(13,12),Tile,false);
                tach[i].rectTransform.localRotation = Quaternion.Euler(0,0,-8);
            }
            for (int i = 0; i < 5; i++)
                Label(drive,(i * 2 + 1).ToString(),bottomRight,new Vector2(-341+i*67.5f,153),new Vector2(18,16),11,Muted,TextAnchor.MiddleCenter,FontStyle.Bold);
            ImageArea("Instrument divider",drive,bottomRight,new Vector2(-206,140),new Vector2(286,1),new Color(.39f,.46f,.54f,.45f),false);
            speed = Label(drive,"128",bottomRight,new Vector2(-241,98),new Vector2(212,75),76,Type,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic);
            Label(drive,"KM/H",bottomRight,new Vector2(-302,52),new Vector2(90,20),13,Muted,TextAnchor.MiddleLeft,FontStyle.Bold);
            ImageArea("Gear pod",drive,bottomRight,new Vector2(-93,86),new Vector2(63,72),new Color(.11f,.15f,.20f),false);
            Label(drive,"GEAR",bottomRight,new Vector2(-93,110),new Vector2(54,17),11,Muted,TextAnchor.MiddleCenter,FontStyle.Bold);
            gear = Label(drive,"3",bottomRight,new Vector2(-93,80),new Vector2(54,40),31,Type,TextAnchor.MiddleCenter,FontStyle.Bold);
            toast = Label(drive,"",new Vector2(.5f,0),new Vector2(0,162),new Vector2(420,36),22,Accent,TextAnchor.MiddleCenter,FontStyle.Bold);

            eventTitle = Label(drive, "", new Vector2(.5f,1), new Vector2(0,-113),
                new Vector2(460,31), 24, Type, TextAnchor.MiddleCenter, FontStyle.Bold);
            eventDetail = Label(drive, "", new Vector2(.5f,1), new Vector2(0,-146),
                new Vector2(510,24), 17, Accent, TextAnchor.MiddleCenter);
            draftStatus = Label(drive, "", new Vector2(.5f,0), new Vector2(0,200),
                new Vector2(500,27), 18, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);

            menu = Panel("Main menu");
            Label(menu,"MOUNTAIN ROUTE   /   00:42 AM",new Vector2(0,.5f),new Vector2(305,169),new Vector2(418,32),16,Accent,TextAnchor.MiddleLeft,FontStyle.Bold);
            Label(menu,"NIGHTSHIFT",new Vector2(0,.5f),new Vector2(490,88),new Vector2(792,127),111,Type,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic);
            Label(menu,"I N F I N I T E   H I G H W A Y",new Vector2(0,.5f),new Vector2(365,15),new Vector2(526,40),27,Type,TextAnchor.MiddleLeft);
            Label(menu,"Thread through midnight traffic. Collect coins,\nrefuel on the move and keep the run alive.",new Vector2(0,.5f),
                new Vector2(365,-55),new Vector2(526,68),21,Muted,TextAnchor.MiddleLeft);
            mainAction = Action(menu,"START DRIVE",new Vector2(0,.5f),new Vector2(201,-132),new Vector2(198,53),true,game.StartRun);
            Action(menu,"MIDNIGHT DUEL",new Vector2(0,.5f),new Vector2(411,-132),new Vector2(198,53),false,game.StartDuel);
            Action(menu,"TIME TRIAL",new Vector2(0,.5f),new Vector2(621,-132),new Vector2(198,53),false,game.StartTimeTrial);
            Action(menu,"GARAGE",new Vector2(0,.5f),new Vector2(201,-196),new Vector2(198,53),false,game.OpenGarage);
            var course = Action(menu,"COURSE MODE", new Vector2(0,.5f),new Vector2(411,-196),new Vector2(198,53),false,game.ToggleCourseMode);
            Action(menu,"EXIT",new Vector2(0,.5f),new Vector2(621,-196),new Vector2(198,53),false,game.Exit);
            courseMode = course.GetComponentInChildren<Text>();
            Label(menu,"A / D   CHANGE LANES     W   NITRO     S   BRAKE     ESC   PAUSE",new Vector2(0,0),new Vector2(419,45),
                new Vector2(632,30),16,Muted,TextAnchor.MiddleLeft);
            selectedCar = Label(menu,"",new Vector2(1,0),new Vector2(-285,62),new Vector2(500,45),28,Type,TextAnchor.MiddleRight,FontStyle.BoldAndItalic);

            paused = Panel("Pause menu");
            Label(paused,"RUN SUSPENDED",new Vector2(0,.5f),new Vector2(303,98),new Vector2(408,32),16,Accent,TextAnchor.MiddleLeft,FontStyle.Bold);
            Label(paused,"PAUSED",new Vector2(0,.5f),new Vector2(477,20),new Vector2(758,118),96,Type,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic);
            resumeAction = Action(paused,"RESUME",new Vector2(0,.5f),new Vector2(173,-82),new Vector2(151,53),true,game.TogglePause);
            Action(paused,"RESTART",new Vector2(0,.5f),new Vector2(347,-82),new Vector2(151,53),false,game.StartRun);
            Action(paused,"MAIN MENU",new Vector2(0,.5f),new Vector2(521,-82),new Vector2(151,53),false,game.ToMenu);

            ended = Panel("Game over");
            finalReason = Label(ended,"RUN ENDED",new Vector2(0,.5f),new Vector2(303,110),new Vector2(410,32),16,Accent,TextAnchor.MiddleLeft,FontStyle.Bold);
            Label(ended,"RUN OVER",new Vector2(0,.5f),new Vector2(477,32),new Vector2(758,118),94,Type,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic);
            finalResult = Label(ended,"",new Vector2(0,.5f),new Vector2(388,-54),new Vector2(580,39),24,Muted,TextAnchor.MiddleLeft);
            restartAction = Action(ended,"DRIVE AGAIN",new Vector2(0,.5f),new Vector2(188,-120),new Vector2(181,53),true,game.StartRun);
            Action(ended,"MAIN MENU",new Vector2(0,.5f),new Vector2(373,-120),new Vector2(168,53),false,game.ToMenu);
            replayAction = Action(ended,"WATCH REPLAY",new Vector2(0,.5f),new Vector2(558,-120),new Vector2(183,53),false,game.StartReplay);
            replay = Full("Replay overlay", screen);
            Label(replay, "OVERTAKE REPLAY", new Vector2(.5f,1), new Vector2(0,-55),
                new Vector2(435,38), 28, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            replayExit = Action(replay, "SKIP REPLAY", new Vector2(1,0), new Vector2(-160,60),
                new Vector2(164,52), false, game.StopReplay);

            Label(screen,"///   N I G H T S H I F T",new Vector2(0,1),new Vector2(223,-45),new Vector2(354,36),21,Accent,TextAnchor.MiddleLeft,FontStyle.Bold);
            var muteButton = Action(screen,"SOUND ON",new Vector2(1,1),new Vector2(-187,-46),new Vector2(125,46),false,game.ToggleMute);
            mute = muteButton.GetComponentInChildren<Text>();
            pauseButton = Action(screen,"PAUSE",new Vector2(1,1),new Vector2(-83,-46),new Vector2(76,46),false,game.TogglePause);
        }

        public void Refresh()
        {
            GameState state = game.State;
            menu.gameObject.SetActive(state == GameState.Menu);
            replay.gameObject.SetActive(state == GameState.Replay);
            replayAction.gameObject.SetActive(game.HasReplay);
            selectedCar.text = game.Vehicle.DisplayName.ToUpperInvariant();
            paused.gameObject.SetActive(state == GameState.Paused);
            ended.gameObject.SetActive(state == GameState.GameOver);
            drive.gameObject.SetActive(state == GameState.Playing || state == GameState.Paused);
            pauseButton.gameObject.SetActive(state == GameState.Playing);
            mute.text = game.Muted ? "SOUND OFF" : "SOUND ON";
            courseMode.text = game.CourseMode ? "COURSE MODE · ON" : "COURSE MODE";
            if (!refreshed || previous != state)
            {
                if (state == GameState.Menu) input.SetSelectedGameObject(mainAction.gameObject);
                else if (state == GameState.Paused) input.SetSelectedGameObject(resumeAction.gameObject);
                else if (state == GameState.GameOver) input.SetSelectedGameObject(restartAction.gameObject);
                else if (state == GameState.Replay) input.SetSelectedGameObject(replayExit.gameObject);
                else input.SetSelectedGameObject(null);
            }
            garage.Refresh(state == GameState.Garage);
            refreshed = true; previous = state;
            Tick();
        }

        public void Tick()
        {
            if (game.State == GameState.GameOver)
            {
                finalReason.text = game.EndReason.ToUpperInvariant();
                finalResult.text = game.Mode == RunMode.TimeTrial
                    ? $"TIME  {game.RunTime:0.00}s       BEST  {(game.HasGhost ? game.TrialBest.ToString("0.00") + "s" : "--")}"
                    : game.Mode == RunMode.Duel
                        ? $"{game.RivalName.ToUpperInvariant()}  {game.RivalGap:+0.0;-0.0;0.0} M       SCORE  {game.Score:0000}"
                        : $"SCORE   {game.Score:0000}        BEST   {game.Best:0000}        {game.Distance / 1000:0.00} KM";
            }
            score.text = game.Score.ToString("0000"); best.text = game.Best.ToString("0000");
            km.text = (game.Distance / 1000).ToString("0.00");
            float kmh = game.Speed * 3.6f;
            int currentGear = Transmission.GearIndex(kmh);
            speed.text = Mathf.RoundToInt(kmh).ToString("000");
            gear.text = (currentGear + 1).ToString();
            int lit = Mathf.CeilToInt(tach.Length * Transmission.Revolutions(kmh, currentGear) / 9000);
            for (int i = 0; i < tach.Length; i++)
                tach[i].color = i < lit
                    ? i >= 15 ? new Color(.89f,.54f,.47f) : new Color(.84f,.88f,.92f)
                    : i >= 15 ? new Color(.31f,.21f,.23f) : new Color(.22f,.27f,.33f);
            route.text = zones[game.Zone];
            life.text = (game.Lives > 0 ? "♥" : "♡") + "  " + (game.Lives > 1 ? "♥" : "♡") + "  " + (game.Lives > 2 ? "♥" : "♡");
            fuelNumber.text = Mathf.CeilToInt(game.Fuel) + "%";
            fuelFill.rectTransform.sizeDelta = new Vector2(252 * game.Fuel / 100, 8);
            fuelFill.color = game.Fuel < 23 ? new Color(.88f,.37f,.43f) : Accent;
            nitroLabel.text = game.NitroEmptyWhileHeld ? "NITRO · RELEASE W" : "NITRO · W";
            nitroNumber.text = Mathf.CeilToInt(game.Nitro) + "%";
            nitroFill.rectTransform.sizeDelta = new Vector2(252 * game.Nitro / 100, 8);
            toast.gameObject.SetActive(game.MessageVisible);
            toast.text = game.Message;
            eventTitle.text = game.Mode == RunMode.Duel ? $"MIDNIGHT DUEL   {Mathf.CeilToInt(game.TimeLeft):00}s"
                : game.Mode == RunMode.TimeTrial ? $"TIME TRIAL   {game.RunTime:0.00}s" : "";
            eventDetail.text = game.Mode == RunMode.Duel ? $"{game.RivalName.ToUpperInvariant()}    {game.RivalGap:+0;-0;0} M"
                : game.Mode == RunMode.TimeTrial ? $"{game.Distance / 1000:0.00}/{game.TrialLength / 1000:0.00} KM"
                    + (game.HasGhost ? $"    GHOST {game.GhostDelta:+0.0;-0.0;0.0}s" : "    SET YOUR FIRST GHOST") : "";
            draftStatus.text = game.Drafting ? "SLIPSTREAM   •   NITRO RECHARGING" : game.Combo > 1 ? $"CLEAN PASS COMBO  ×{game.Combo}" : "";
        }

        public void Dispose() => garage.Dispose();

        public void SetImpact(float value) => impact.color = new Color(.95f,.09f,.16f,value*.46f);

        RectTransform Panel(string name)
        {
            var panel = Full(name, screen);
            var shade = ImageArea("Scrim",panel,Vector2.zero,Vector2.zero,Vector2.zero,Color.white,false);
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = Vector2.one;
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            var tex = new Texture2D(128,1,TextureFormat.RGBA32,false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 128; i++)
            {
                float alpha = Mathf.Lerp(.97f, 0, Mathf.SmoothStep(0,1,Mathf.Clamp01((i-15)/98f)));
                tex.SetPixel(i,0,new Color(.022f,.031f,.055f,alpha));
            }
            tex.Apply(false,true);
            shade.sprite = Sprite.Create(tex,new Rect(0,0,128,1),new Vector2(.5f,.5f));
            return panel;
        }

        Text Metric(RectTransform parent, string label, Vector2 at)
        {
            ImageArea(label+" tile",parent,Vector2.up,at+new Vector2(56,-30),new Vector2(112,76),Tile,false);
            Label(parent,label,Vector2.up,at+new Vector2(62,-11),new Vector2(101,20),13,Muted,TextAnchor.MiddleLeft,FontStyle.Bold);
            return Label(parent,"0",Vector2.up,at+new Vector2(62,-40),new Vector2(101,34),26,Type,TextAnchor.MiddleLeft,FontStyle.Bold);
        }

        Button Action(RectTransform root, string text, Vector2 anchor, Vector2 at, Vector2 size,bool primary,UnityEngine.Events.UnityAction click)
        {
            var background = ImageArea(text,root,anchor,at,size,primary ? Accent : Tile,true);
            var label = Label(background.rectTransform,text,new Vector2(.5f,.5f),Vector2.zero,size,16,
                primary ? new Color(.054f,.09f,.039f) : Type,TextAnchor.MiddleCenter,FontStyle.Bold);
            label.raycastTarget = false;
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors; colors.selectedColor = new Color(.79f,.88f,1); colors.highlightedColor = new Color(.87f,.97f,1);
            colors.pressedColor = new Color(.68f,.79f,.83f); button.colors = colors;
            button.onClick.AddListener(click);
            return button;
        }

        Text Label(RectTransform root,string text,Vector2 anchor,Vector2 pos,Vector2 size,int points,Color color,TextAnchor align,FontStyle style=FontStyle.Normal)
        {
            var r = Area(text.Length > 0 ? text : "Text",root,anchor,pos,size);
            var label = r.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = points; label.color = color; label.alignment = align; label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow; label.raycastTarget = false;
            return label;
        }

        static Image ImageArea(string name,RectTransform root,Vector2 anchor,Vector2 pos,Vector2 size,Color color,bool raycast)
        {
            var result = Area(name,root,anchor,pos,size).gameObject.AddComponent<Image>();
            result.color = color; result.raycastTarget = raycast; return result;
        }

        static RectTransform Area(string name,RectTransform root,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            var result = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(root,false); result.anchorMin = result.anchorMax = anchor;
            result.sizeDelta = size; result.anchoredPosition = pos; return result;
        }

        static RectTransform Full(string name,RectTransform root)
        {
            var r = Area(name,root,Vector2.zero,Vector2.zero,Vector2.zero);
            r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }
    }
}
