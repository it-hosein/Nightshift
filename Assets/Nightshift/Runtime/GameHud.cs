using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Nightshift
{
    public sealed class GameHud
    {
        static readonly Color Accent = new Color(.67f,.98f,.41f);
        static readonly Color Type = new Color(.93f,.96f,1);
        static readonly Color Muted = new Color(.71f,.79f,.87f);
        static readonly Color Tile = new Color(.032f,.056f,.083f,.85f);
        readonly GameManager game;
        readonly Font font;
        readonly Canvas canvas;
        readonly RectTransform screen;
        readonly RectTransform menu, paused, ended, drive;
        readonly Text score, best, km, speed, route, life, fuelNumber, toast, finalReason, finalResult, mute;
        readonly Image fuelFill, impact;
        readonly Button mainAction, resumeAction, restartAction, pauseButton;
        readonly string[] zones = { "ALPINE PASS", "GLACIER TUNNEL", "RAVEN BRIDGE" };
        readonly EventSystem input;
        GameState previous;
        bool refreshed;

        public GameHud(GameManager game)
        {
            this.game = game;
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
            life = Label(drive, "♥  ♥  ♥", new Vector2(0,0),new Vector2(171,100),new Vector2(250,38),30,new Color(1,.31f,.43f),TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(drive, "FUEL",new Vector2(0,0),new Vector2(89,62),new Vector2(86,25),15,Muted,TextAnchor.MiddleLeft);
            fuelNumber = Label(drive,"100%",new Vector2(0,0),new Vector2(272,62),new Vector2(52,25),15,Type,TextAnchor.MiddleRight);
            ImageArea("Fuel track",drive,new Vector2(0,0),new Vector2(172,44),new Vector2(252,8),new Color(.30f,.37f,.46f,.76f),false);
            fuelFill = ImageArea("Fuel remaining",drive,new Vector2(0,0),new Vector2(46,44),new Vector2(252,8),Accent,false);
            fuelFill.rectTransform.pivot = new Vector2(0,.5f); fuelFill.rectTransform.anchoredPosition = new Vector2(46,44);
            speed = Label(drive,"128",new Vector2(1,0),new Vector2(-158,114),new Vector2(223,112),95,Type,TextAnchor.MiddleRight,FontStyle.BoldAndItalic);
            Label(drive,"KM/H",new Vector2(1,0),new Vector2(-157,54),new Vector2(222,26),16,Muted,TextAnchor.MiddleRight,FontStyle.Bold);
            ImageArea("Speed underline",drive,new Vector2(1,0),new Vector2(-119,35),new Vector2(146,3),Accent,false);
            toast = Label(drive,"",new Vector2(.5f,0),new Vector2(0,162),new Vector2(420,36),22,Accent,TextAnchor.MiddleCenter,FontStyle.Bold);

            menu = Panel("Main menu");
            Label(menu,"MOUNTAIN ROUTE   /   00:42 AM",new Vector2(0,.5f),new Vector2(305,169),new Vector2(418,32),16,Accent,TextAnchor.MiddleLeft,FontStyle.Bold);
            Label(menu,"NIGHTSHIFT",new Vector2(0,.5f),new Vector2(490,88),new Vector2(792,127),111,Type,TextAnchor.MiddleLeft,FontStyle.BoldAndItalic);
            Label(menu,"I N F I N I T E   H I G H W A Y",new Vector2(0,.5f),new Vector2(365,15),new Vector2(526,40),27,Type,TextAnchor.MiddleLeft);
            Label(menu,"Thread through midnight traffic. Collect coins,\nrefuel on the move and keep the run alive.",new Vector2(0,.5f),
                new Vector2(365,-55),new Vector2(526,68),21,Muted,TextAnchor.MiddleLeft);
            mainAction = Action(menu,"START DRIVE",new Vector2(0,.5f),new Vector2(201,-132),new Vector2(198,53),true,game.StartRun);
            Action(menu,"EXIT",new Vector2(0,.5f),new Vector2(372,-132),new Vector2(124,53),false,game.Exit);
            Label(menu,"A / D   CHANGE LANES     W   THROTTLE     S   BRAKE     ESC   PAUSE",new Vector2(0,0),new Vector2(419,45),
                new Vector2(632,30),16,Muted,TextAnchor.MiddleLeft);
            Label(menu,"FERRARI 458 ITALIA",new Vector2(1,0),new Vector2(-245,62),new Vector2(370,45),32,Type,TextAnchor.MiddleRight,FontStyle.BoldAndItalic);

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

            Label(screen,"///   N I G H T S H I F T",new Vector2(0,1),new Vector2(223,-45),new Vector2(354,36),21,Accent,TextAnchor.MiddleLeft,FontStyle.Bold);
            var muteButton = Action(screen,"SOUND ON",new Vector2(1,1),new Vector2(-187,-46),new Vector2(125,46),false,game.ToggleMute);
            mute = muteButton.GetComponentInChildren<Text>();
            pauseButton = Action(screen,"PAUSE",new Vector2(1,1),new Vector2(-83,-46),new Vector2(76,46),false,game.TogglePause);
        }

        public void Refresh()
        {
            GameState state = game.State;
            menu.gameObject.SetActive(state == GameState.Menu);
            paused.gameObject.SetActive(state == GameState.Paused);
            ended.gameObject.SetActive(state == GameState.GameOver);
            drive.gameObject.SetActive(state == GameState.Playing || state == GameState.Paused);
            pauseButton.gameObject.SetActive(state == GameState.Playing);
            mute.text = game.Muted ? "SOUND OFF" : "SOUND ON";
            if (!refreshed || previous != state)
            {
                if (state == GameState.Menu) input.SetSelectedGameObject(mainAction.gameObject);
                else if (state == GameState.Paused) input.SetSelectedGameObject(resumeAction.gameObject);
                else if (state == GameState.GameOver) input.SetSelectedGameObject(restartAction.gameObject);
                else input.SetSelectedGameObject(null);
            }
            refreshed = true; previous = state;
            Tick();
        }

        public void Tick()
        {
            if (game.State == GameState.GameOver)
            {
                finalReason.text = game.EndReason.ToUpperInvariant();
                finalResult.text = $"SCORE   {game.Score:0000}        BEST   {game.Best:0000}        {game.Distance / 1000:0.00} KM";
            }
            score.text = game.Score.ToString("0000"); best.text = game.Best.ToString("0000");
            km.text = (game.Distance / 1000).ToString("0.00"); speed.text = Mathf.RoundToInt(game.Speed * 3.6f).ToString();
            route.text = zones[game.Zone];
            life.text = (game.Lives > 0 ? "♥" : "♡") + "  " + (game.Lives > 1 ? "♥" : "♡") + "  " + (game.Lives > 2 ? "♥" : "♡");
            fuelNumber.text = Mathf.CeilToInt(game.Fuel) + "%";
            fuelFill.rectTransform.sizeDelta = new Vector2(252 * game.Fuel / 100, 8);
            fuelFill.color = game.Fuel < 23 ? new Color(1,.29f,.38f) : Accent;
            toast.gameObject.SetActive(game.MessageVisible);
            toast.text = game.Message;
        }

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
