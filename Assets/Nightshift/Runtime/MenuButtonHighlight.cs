using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nightshift
{
    sealed class MenuButtonHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        Image background, rail, edge;
        Text title, detail, arrow;
        bool primary, hover, selected;

        public void Configure(Image background, Image rail, Image edge, Text title, Text detail, Text arrow, bool primary)
        {
            this.background = background;
            this.rail = rail;
            this.edge = edge;
            this.title = title;
            this.detail = detail;
            this.arrow = arrow;
            this.primary = primary;
            Paint();
        }

        public void OnPointerEnter(PointerEventData data) { hover = true; Paint(); }
        public void OnPointerExit(PointerEventData data) { hover = false; Paint(); }
        public void OnSelect(BaseEventData data) { selected = true; Paint(); }
        public void OnDeselect(BaseEventData data) { selected = false; Paint(); }

        void OnDisable()
        {
            hover = selected = false;
            Paint();
        }

        void Paint()
        {
            if (background == null) return;
            Color focus = new Color(.96f, .74f, .43f);
            Color pointer = new Color(.57f, .94f, .89f);
            Color idle = new Color(.29f, .44f, .5f, .74f);
            Color mark = selected ? focus : hover ? pointer : idle;
            background.color = selected ? new Color(.24f, .174f, .108f, .99f)
                : hover ? new Color(.10f, .28f, .31f, .98f)
                : primary ? new Color(.11f, .21f, .23f, .98f)
                : new Color(.047f, .082f, .118f, .94f);
            edge.color = mark;
            rail.color = selected || hover ? mark : primary ? pointer : idle;
            title.color = selected ? new Color(1, .91f, .72f) : new Color(.94f, .97f, 1);
            if (detail != null) detail.color = selected ? new Color(.90f, .75f, .58f) : new Color(.67f, .78f, .85f);
            arrow.text = selected || hover ? "›" : "";
            arrow.color = mark;
        }
    }
}
