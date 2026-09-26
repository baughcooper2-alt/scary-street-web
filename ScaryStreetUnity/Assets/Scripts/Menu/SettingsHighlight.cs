using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows which settings row you're on: a soft bar behind the row, a mustard stripe at its left and a gold label,
// following the controller's selection (and the mouse: hovering a row selects it). GameSettings adds the rows.
public class SettingsHighlight : MonoBehaviour
{
    class Row { public Selectable control; public Image bar, stripe; public Text label; }
    readonly List<Row> rows = new List<Row>();

    public void Add(Selectable control, Image bar, Image stripe, Text label)
    {
        rows.Add(new Row { control = control, bar = bar, stripe = stripe, label = label });
        // hovering the row (its label or its control) selects it, so the mouse moves the highlight too
        foreach (var g in new[] { control.gameObject, label.gameObject })
        {
            var trig = g.GetComponent<EventTrigger>() ? g.GetComponent<EventTrigger>() : g.AddComponent<EventTrigger>();
            var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            e.callback.AddListener(_ => { if (EventSystem.current) EventSystem.current.SetSelectedGameObject(control.gameObject); });
            trig.triggers.Add(e);
        }
        label.raycastTarget = true;
        Paint(rows[rows.Count - 1], false);
    }

    void Update()
    {
        var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var r in rows) Paint(r, r.control && sel == r.control.gameObject);
    }

    static void Paint(Row r, bool on)
    {
        r.bar.enabled = on; r.stripe.enabled = on;
        r.label.color = on ? UIArt.Theme.Mustard : UIArt.Theme.Paper;
    }
}
