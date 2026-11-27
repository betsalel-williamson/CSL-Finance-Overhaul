using System.Collections; // IEnumerator
using System.Collections.Generic; // List
// ColossalFramework.UI: LayoutDirection, UIButton, UIComponent, UILabel, UIScrollablePanel,
//   UISlider, UITextField, UIView
using ColossalFramework.UI;
using FinanceOverhaul.Core; // IpoRules, IpoTuning, IpoUserPreset, ModSettings
using UnityEngine; // Object, RectOffset, Vector2, Vector3, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): UIHelper

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: the Local IPOs options (types, presets, the preview) and putting them back.</summary>
    public partial class UiTestRunner
    {
        private IEnumerator IpoOptionSteps(
            string line,
            string cmd,
            string a,
            string b,
            Context ctx,
            StepResult r
        )
        {
            switch (cmd)
            {
                case "options-preview":
                {
                    // options-preview on|off|scroll Y — builds the Options page (Options → Finance
                    // Overhaul) into a panel over the game, as the game would, so a scenario can
                    // read and capture it; logs how many Local IPOs sliders it has. scroll Y
                    // scrolls the preview down Y pixels.
                    if (a == "scroll")
                    {
                        var shown = FindControl(OptionsPreviewName, true) as UIScrollablePanel;
                        string[] parts = line.Substring(cmd.Length).Trim().Split(' ');
                        if (shown != null && parts.Length == 2)
                        {
                            shown.scrollPosition = new Vector2(0f, ParseFloat(parts[1], 0f));
                        }

                        yield return new WaitForSeconds(0.3f);
                        break;
                    }

                    UIComponent old = FindControl(OptionsPreviewName, true);
                    if (old != null)
                    {
                        Object.Destroy(old.gameObject);
                    }

                    if (a != "on")
                    {
                        break;
                    }

                    UIView view = UIView.GetAView();
                    var root = view.AddUIComponent(typeof(UIScrollablePanel)) as UIScrollablePanel;
                    root.name = OptionsPreviewName;
                    root.backgroundSprite = "MenuPanel2"; // not shown: a game sprite name
                    root.size = new Vector2(1860f, 1080f);
                    root.relativePosition = new Vector3(30f, 40f);
                    root.autoLayout = true;
                    root.autoLayoutDirection = LayoutDirection.Horizontal;
                    root.wrapLayout = true;
                    root.autoLayoutPadding = new RectOffset(10, 10, 10, 0);
                    root.clipChildren = true;
                    ModOptions.Build(new UIHelper(root));
                    int sliders = 0;
                    foreach (UISlider s in root.GetComponentsInChildren<UISlider>())
                    {
                        if (s.name.StartsWith("FinanceOverhaulIpoOption-"))
                        {
                            sliders++;
                        }
                    }

                    ModLog.Info("uitest: options preview has " + sliders + " Local IPOs slider(s)");
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "ipo-option-type":
                {
                    // ipo-option-type KEY TEXT|restore — types TEXT into that option's number
                    // field in the Options preview and submits it (restore: the value it had
                    // before this scenario first typed into it). Logs the field afterwards.
                    string[] parts = line.Substring(cmd.Length).Trim().Split(new[] { ' ' }, 2);
                    IpoTuning t = IpoTuning.Find(parts[0]);
                    if (t == null || parts.Length < 2)
                    {
                        r.Error = "ipo-option-type KEY TEXT";
                        break;
                    }

                    ModSettings settings = Mod.EnsureSettings();
                    if (!typedBefore.ContainsKey(t.Key))
                    {
                        typedBefore[t.Key] = t.Get(settings.Ipo);
                    }

                    string text =
                        parts[1] == "restore" ? t.Shown(typedBefore[t.Key]).ToString() : parts[1];
                    string shown = IpoOptions.TestType(t.Key, text);
                    if (shown == null)
                    {
                        r.Error = "no number field for " + t.Key + " (options-preview on first)";
                        break;
                    }

                    if (parts[1] == "restore")
                    {
                        typedBefore.Remove(t.Key);
                    }

                    ModLog.Info(
                        "uitest: typed '" + text + "' into " + t.Key + "; it shows " + shown
                    );
                    break;
                }
                case "expect-ipo-option":
                {
                    // expect-ipo-option KEY VALUE — the option's value as the player sees it,
                    // in the settings, in its label and in its number field.
                    string[] parts = line.Substring(cmd.Length).Trim().Split(' ');
                    IpoTuning t = IpoTuning.Find(parts[0]);
                    int want;
                    if (t == null || parts.Length != 2 || !int.TryParse(parts[1], out want))
                    {
                        r.Error = "expect-ipo-option KEY VALUE";
                        break;
                    }

                    int have = t.Shown(t.Get(Mod.EnsureSettings().Ipo));
                    UITextField field =
                        FindControl(IpoOptions.FieldPrefix + t.Key, true) as UITextField;
                    if (have != want)
                    {
                        r.Error = t.Key + " is " + have + ", expected " + want;
                    }
                    else if (field == null || field.text != t.FieldText(Mod.EnsureSettings().Ipo))
                    {
                        r.Error =
                            t.Key + "'s field reads '" + (field == null ? "-" : field.text) + "'";
                    }

                    break;
                }
                case "ipo-options-keep":
                case "ipo-options-put-back":
                {
                    // ipo-options-keep — remembers the player's Local IPOs values and saved
                    // presets; ipo-options-put-back puts them back (and the page shows them).
                    ModSettings settings = Mod.EnsureSettings();
                    if (cmd == "ipo-options-keep")
                    {
                        keptValues = new IpoRules();
                        foreach (IpoTuning t in IpoTuning.All)
                        {
                            t.Set(keptValues, t.Get(settings.Ipo));
                        }

                        keptPresets = new List<IpoUserPreset>(settings.IpoUserPresets);
                    }
                    else if (keptValues != null)
                    {
                        IpoOptions.TestPutBack(keptValues, keptPresets);
                        keptValues = null;
                    }

                    break;
                }
                case "ipo-section":
                {
                    // ipo-section GROUP LessLikely|Default|MoreLikely — clicks that section's button.
                    string[] parts = line.Substring(cmd.Length).Trim().Split(' ');
                    UIButton sectionButton =
                        parts.Length == 2
                            ? FindControl(
                                IpoOptions.SectionButtonPrefix + parts[0] + "-" + parts[1],
                                true
                            ) as UIButton
                            : null;
                    if (sectionButton == null)
                    {
                        r.Error = "no such section button (options-preview on first)";
                        break;
                    }

                    sectionButton.SimulateClick();
                    break;
                }
                case "ipo-preset-save":
                {
                    // ipo-preset-save NAME — Save as…, types NAME and clicks OK.
                    string name = line.Substring(cmd.Length).Trim();
                    UIButton save = FindControl(IpoOptions.SaveAsName, true) as UIButton;
                    if (save == null)
                    {
                        r.Error = "no Save as… button (options-preview on first)";
                        break;
                    }

                    save.SimulateClick();
                    yield return new WaitForSeconds(0.3f);
                    if (FoNameDialog.Field == null)
                    {
                        r.Error = "the name dialog did not open";
                        break;
                    }

                    FoNameDialog.Field.text = name;
                    FoNameDialog.TestOkOpen();
                    yield return new WaitForSeconds(0.3f);
                    if (FoNameDialog.IsOpen)
                    {
                        ModLog.Info("uitest: the name dialog stayed open for '" + name + "'");
                        FoNameDialog.Close();
                    }

                    break;
                }
                case "ipo-preset-delete":
                {
                    UIButton delete = FindControl(IpoOptions.DeleteName, true) as UIButton;
                    if (delete == null)
                    {
                        r.Error = "no Delete button (options-preview on first)";
                        break;
                    }

                    delete.SimulateClick();
                    break;
                }
                case "expect-ipo-preset":
                {
                    // expect-ipo-preset NAME — the presets dropdown shows NAME.
                    string want = line.Substring(cmd.Length).Trim();
                    string shown = IpoOptions.ShownPreset;
                    ModLog.Info("uitest: preset shown: " + shown);
                    if (shown != want)
                    {
                        r.Error = "the preset shown is '" + shown + "', expected '" + want + "'";
                    }

                    break;
                }
                case "expect-ipo-options":
                {
                    // expect-ipo-options N — the Options preview shows N Local IPOs sliders, each
                    // label saying its value.
                    UIComponent root = FindControl(OptionsPreviewName, true);
                    if (root == null)
                    {
                        r.Error = "options-preview on first";
                        break;
                    }

                    int found = 0;
                    foreach (UISlider s in root.GetComponentsInChildren<UISlider>())
                    {
                        if (!s.name.StartsWith("FinanceOverhaulIpoOption-"))
                        {
                            continue;
                        }

                        found++;
                        UITextField field = s.parent.Find<UITextField>(
                            IpoOptions.FieldPrefix
                                + s.name.Substring("FinanceOverhaulIpoOption-".Length)
                        );
                        if (field == null)
                        {
                            r.Error = s.name + " has no number field";
                            break;
                        }

                        UILabel label = s.parent.Find<UILabel>("Label");
                        IpoTuning t = IpoTuning.Find(
                            s.name.Substring("FinanceOverhaulIpoOption-".Length)
                        );
                        string want = t == null ? null : t.Label(CompanyDesk.Current);
                        if (label == null || label.text != want)
                        {
                            r.Error =
                                s.name + " reads '" + (label == null ? "-" : label.text) + "'";
                            break;
                        }
                    }

                    ModLog.Info("uitest: " + found + " Local IPOs slider(s) with their values");
                    if (r.Error == null && found != (int)ParseFloat(a, -1f))
                    {
                        r.Error = found + " Local IPOs sliders, expected " + a;
                    }

                    break;
                }
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }
    }
}
