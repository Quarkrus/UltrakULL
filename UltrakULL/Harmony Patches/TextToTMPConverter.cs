using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UltrakULL.CommonFunctions;
using UltrakULL;

namespace UltrakULL.Harmony_Patches
{
    public static class TextToTMPConverter
    {
        private static readonly Dictionary<int, TextMeshProUGUI> textToTMP = new Dictionary<int, TextMeshProUGUI>();
        private static readonly Dictionary<int, Text> tmpToSourceText = new Dictionary<int, Text>();
        private static readonly string[] universeLibPatterns =
        {
            "UniverseLibCanvas",
            "unityexplorer",
            "com.sinai",
            "UniverseLib",
            "ExplorerCanvas",
            "InspectorCanvas",
            "MouseInspectDropdown",
            "Dropdown List",
            "Viewport",
            "Inspector",
            "PanelHolder"
        };
        // The legacy intermission shadow uses an opaque black Text.  Cap it to
        // the same softer opacity used by the generated TMP shadows.
        private const float IntermissionShadowAlpha = 0.45f;
        private static readonly Vector2 IntermissionShadowOffset = new Vector2(2f, -2.75f);

        // Public API for reverse lookup: TMP -> original Text
        public static Text GetSourceText(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return null;

            int tmpId = tmp.GetInstanceID();
            if (tmpToSourceText.TryGetValue(tmpId, out Text sourceText) && sourceText != null)
                return sourceText;

            tmpToSourceText.Remove(tmpId);
            return null;
        }

        // Better reverse lookup using TMP name pattern
        public static bool IsConvertedFromHealthBar(TextMeshProUGUI tmp)
        {
            Text sourceText = GetSourceText(tmp);
            if (sourceText == null)
                return false;

            HealthBar healthBar = sourceText.transform.GetComponentInParent<HealthBar>();
            return healthBar != null && healthBar.hpText == sourceText;
        }

        [HarmonyPatch(typeof(Text), "OnEnable")]
        public static class TextOnEnablePatch
        {
            [HarmonyPostfix]
            public static void Postfix(Text __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                ConvertTextToTMP(__instance);
            }
        }

        [HarmonyPatch(typeof(Text), "set_text")]
        public static class TextSetTextPatch
        {
            [HarmonyPostfix]
            public static void Postfix(Text __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                UpdateTMPText(__instance);
            }
        }

        [HarmonyPatch(typeof(Text), "OnDisable")]
        public static class TextOnDisablePatch
        {
            [HarmonyPostfix]
            public static void Postfix(Text __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                SetTMPActiveState(__instance, false);
                SyncTextShadowTwin(__instance, GetTMPForSource(__instance));
            }
        }

        private static void ConvertTextToTMP(Text source)
        {
            if (source == null)
            {
                return;
            }

            bool isIntermissionShadowSource = IsIntermissionShadowSource(source);

            // Skip conversion for UniverseLibCanvas texts (UnityExplorer UI)
            if (IsUniverseLibCanvas(source))
            {
                // Logging.Message($"Skipping conversion for UniverseLibCanvas text: {source.gameObject.name}");
                return;
            }

            TextMeshProUGUI tmp = GetTMPForSource(source);
            if (tmp != null)
            {
                RefreshExistingConvertedText(source, tmp, isIntermissionShadowSource);
                return;
            }

            tmp = CreateTMPSibling(source);
            if (tmp == null)
            {
                return;
            }
            textToTMP[source.GetInstanceID()] = tmp;
            tmpToSourceText[tmp.GetInstanceID()] = source;


            if (IsFishingResultText(source))
            {
                ApplyFishingTMP(source, tmp);
            }
            else
            {
                CopyTextProperties(source, tmp);
            }

            SyncEffects(source, tmp, isIntermissionShadowSource || IsIntermissionShadowChild(source));

            if (Core.TMPFontReady)
            {
                string originalFontName = source.font?.name;

                bool isHealthBarText = source.GetComponentInParent<HealthBar>() != null;
                bool isSpeedometerText = source.GetComponentInParent<Speedometer>() != null;

                bool isInterChild = IsIntermissionShadowChild(source);
                bool forceUnderlay = TMPShadowPolicy.RequiresForcedShadow(source.transform);

                TextMeshProFontSwap.SwapTMPFont(
                    ref tmp,
                    isConvertedFromText: true,
                    originalFontName: originalFontName,
                    forceUnderlay: forceUnderlay,
                    skipIntermissionUnderlay: isIntermissionShadowSource || isInterChild
                );

                // Ensure health and speedometer texts have shadow twins
                if (isHealthBarText || isSpeedometerText)
                {
                    bool altHud = TMPShadowPolicy.IsAltHud(tmp);
                    TMPShadowTwin.EnsureShadow(tmp, altHud ? new Vector2(1.25f, -1.25f) : new Vector2(3f, -3f));

                }
            }

            ApplyIntermissionShadowSourceStyle(source, tmp);

            source.canvasRenderer.SetAlpha(0f);
            SetTMPActiveState(source, source.isActiveAndEnabled);

            if (IsFishingResultText(source))
            {
                ApplyFishingTMP(source, tmp);
            }

            bool forceShadow = TMPShadowPolicy.RequiresForcedShadow(source.transform);
            bool convertedFromHealthText = source.GetComponentInParent<HealthBar>() != null;
            bool convertedFromSpeedometerText = source.GetComponentInParent<Speedometer>() != null;
            if (forceShadow)
                TextMeshProFontSwap.HudControllerPatch.TrackForcedShadowText(tmp);

            if (forceShadow && !Core.TMPFontReady)
                TMPOverlayShadowFallback.Set(tmp, true, new Vector2(1.5f, -1.5f));

            if (convertedFromHealthText || convertedFromSpeedometerText || forceShadow)
            {
                var hud = HudController.Instance;
                if (hud != null)
                {
                    TextMeshProFontSwap.HudControllerPatch.ApplyOverlayZTest(
                        tmp,
                        TextMeshProFontSwap.HudControllerPatch.isOverlaid,
                        hud.overlayTextMaterial,
                        hud.normalTextMaterial,
                        forceManagedShadow: forceShadow || convertedFromHealthText || convertedFromSpeedometerText
                    );
                }
                else if (!Core.TMPFontReady)
                {
                    TMPOverlayShadowFallback.Set(tmp, true, new Vector2(1.5f, -1.5f));
                }
            }

            SyncTextShadowTwin(source, tmp);
        }

        private static void RefreshExistingConvertedText(
            Text source,
            TextMeshProUGUI converted,
            bool isIntermissionShadowSource)
        {
            if (source == null || converted == null)
                return;

            CopyRectTransform(source.rectTransform, converted.rectTransform);
            CopyTextProperties(source, converted);
            SyncEffects(source, converted, isIntermissionShadowSource || IsIntermissionShadowChild(source));
            ApplyIntermissionShadowSourceStyle(source, converted);
            source.canvasRenderer.SetAlpha(0f);

            if (converted.gameObject.activeSelf != source.isActiveAndEnabled)
                converted.gameObject.SetActive(source.isActiveAndEnabled);

            SyncTextShadowTwin(source, converted);
        }

        private static bool IsFishingResultText(Text source)
        {
            return source != null &&
                   (source.name == "Fish Caught Label" ||
                    source.name == "Fish Size Text");
        }

        private static void ApplyFishingTMP(Text source, TextMeshProUGUI tmp)
        {
            if (source == null || tmp == null)
                return;

            tmp.text = source.text;
            tmp.color = source.color;
            tmp.richText = source.supportRichText;
            tmp.alignment = ConvertAlignment(source.alignment);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = false;
            tmp.fontSize = source.fontSize;
            tmp.fontSizeMax = source.fontSize;
            tmp.fontSizeMin = source.fontSize;
            tmp.rectTransform.localScale = Vector3.one;

            tmp.ForceMeshUpdate(true);
        }

        private static void UpdateTMPText(Text source)
        {
            TextMeshProUGUI tmp = GetTMPForSource(source);
            if (tmp == null)
                return;

            if (IsFishingResultText(source))
            {
                ApplyFishingTMP(source, tmp);
                SyncTextShadowTwin(source, tmp);
                return;
            }

            CopyRectTransform(source.rectTransform, tmp.rectTransform);
            CopyTextProperties(source, tmp);
            ApplyIntermissionShadowSourceStyle(source, tmp);
            SyncTextShadowTwin(source, tmp);
            tmp.ForceMeshUpdate();

            if (tmp.rectTransform != null)
                LayoutRebuilder.MarkLayoutForRebuild(tmp.rectTransform);
        }

        private static void SyncTextShadowTwin(Text source, TextMeshProUGUI converted)
        {
            if (source == null || converted == null || source.transform.parent == null)
                return;

            if (SyncIntermissionShadowSource(source))
                return;

            bool sourceIsShadow = IsShadowTextName(source.gameObject.name);
            Text pairedSource = null;
            Transform parent = source.transform.parent;
            Text onlyOppositeRoleCandidate = null;
            int oppositeRoleCandidateCount = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling == source.transform || IsShadowTextName(sibling.name) == sourceIsShadow)
                    continue;

                Text candidate = sibling.GetComponent<Text>();
                if (candidate == null)
                    continue;

                oppositeRoleCandidateCount++;
                onlyOppositeRoleCandidate = candidate;
                if (candidate.text == source.text)
                {
                    pairedSource = candidate;
                    break;
                }
            }

            if (pairedSource == null && oppositeRoleCandidateCount == 1)
                pairedSource = onlyOppositeRoleCandidate;

            if (pairedSource == null)
                return;

            Text mainSource = sourceIsShadow ? pairedSource : source;
            Text shadowSource = sourceIsShadow ? source : pairedSource;
            TextMeshProUGUI mainTMP = GetTMPForSource(mainSource);
            TextMeshProUGUI shadowTMP = GetTMPForSource(shadowSource);
            if (mainTMP == null || shadowTMP == null)
                return;

            shadowTMP.text = mainSource.text;
            shadowTMP.color = TMPShadowTwin.NormalizeGeneratedShadowColor(shadowSource.color);

            if (mainTMP.transform.parent == shadowTMP.transform.parent)
                shadowTMP.transform.SetSiblingIndex(mainTMP.transform.GetSiblingIndex());

            bool shouldShowShadow = mainSource.isActiveAndEnabled &&
                                    shadowSource.isActiveAndEnabled &&
                                    TMPShadowTwin.IsSourceVisible(mainTMP);
            if (shadowTMP.gameObject.activeSelf != shouldShowShadow)
                shadowTMP.gameObject.SetActive(shouldShowShadow);

                Logging.Debug($"[TMPCONV] Paired '{mainSource.name}' with shadow text '{shadowSource.name}'");
        }

        private static bool IsShadowTextName(string name)
        {
                return !string.IsNullOrEmpty(name) &&
                       (name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("тень", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void SetTMPActiveState(Text source, bool active)
        {
            TextMeshProUGUI tmp = GetTMPForSource(source);
            if (tmp == null)
            {
                return;
            }

            if (tmp.gameObject.activeSelf != active)
            {
                tmp.gameObject.SetActive(active);
            }

            if (tmp.rectTransform != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(tmp.rectTransform);
            }
        }

        private static TextMeshProUGUI GetTMPForSource(Text source)
        {
            if (source == null)
            {
                return null;
            }

            int id = source.GetInstanceID();
            if (textToTMP.TryGetValue(id, out TextMeshProUGUI tmp) && tmp != null)
            {
                return tmp;
            }

            textToTMP.Remove(id);
            return null;
        }

        // Public API for external access to converted TMP
        public static TextMeshProUGUI GetConvertedTMP(Text source)
        {
            return GetTMPForSource(source);
        }

        public static bool IsIntermissionShadowSource(Text source)
        {
            if (source == null || source.name != "Text" || source.transform.parent == null)
            {
                return false;
            }

            string sceneName = GetCurrentSceneName();
            if (sceneName != "Intermission1" && sceneName != "Intermission2")
            {
                return false;
            }

            return source.transform.parent.name == "Panel (1)"
                && source.transform.parent.parent != null
                && source.transform.parent.parent.name == "Panel"
                && source.transform.parent.parent.parent != null
                && source.transform.parent.parent.parent.name == "PowerUpVignette"
                && source.transform.parent.parent.parent.parent != null
                && source.transform.parent.parent.parent.parent.name == "Canvas";
        }

        private static void ApplyIntermissionShadowSourceStyle(Text source, TextMeshProUGUI converted)
        {
            if (converted == null ||
                !TryGetIntermissionTextPair(source, out _, out Text shadowSource) ||
                shadowSource != source)
                return;

            converted.text = StripColorTags(converted.text);
            converted.color = GetIntermissionShadowColor(shadowSource.color);
            if (converted.transform.parent == source.transform.parent)
                converted.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
        }

        private static bool SyncIntermissionShadowSource(Text source)
        {
            if (!TryGetIntermissionTextPair(source, out Text mainSource, out Text shadowSource))
                return false;

            TextMeshProUGUI mainTMP = GetTMPForSource(mainSource);
            TextMeshProUGUI shadowTMP = GetTMPForSource(shadowSource);
            if (mainTMP == null || shadowTMP == null)
                return false;

            SyncIntermissionShadowLayout(mainTMP, shadowTMP);
            shadowTMP.text = StripColorTags(mainTMP.text);
            shadowTMP.color = GetIntermissionShadowColor(shadowSource.color);

            bool shouldShowShadow = mainTMP != null &&
                                    mainSource.isActiveAndEnabled &&
                                    shadowSource.isActiveAndEnabled &&
                                    TMPShadowTwin.IsSourceVisible(mainTMP);
            if (shadowTMP.gameObject.activeSelf != shouldShowShadow)
                shadowTMP.gameObject.SetActive(shouldShowShadow);

            return true;
        }

        private static void SyncIntermissionShadowLayout(
            TextMeshProUGUI mainTMP,
            TextMeshProUGUI shadowTMP)
        {
            if (mainTMP == null || shadowTMP == null)
                return;

            Transform mainParent = mainTMP.transform.parent;
            if (mainParent == null)
                return;

            if (shadowTMP.transform.parent != mainParent)
                shadowTMP.transform.SetParent(mainParent, false);

            CopyRectTransform(mainTMP.rectTransform, shadowTMP.rectTransform);
            shadowTMP.rectTransform.anchoredPosition += IntermissionShadowOffset;
            shadowTMP.transform.SetSiblingIndex(mainTMP.transform.GetSiblingIndex());

            shadowTMP.font = mainTMP.font;
            shadowTMP.fontSharedMaterial = mainTMP.fontSharedMaterial;
            shadowTMP.fontSize = mainTMP.fontSize;
            shadowTMP.fontSizeMin = mainTMP.fontSizeMin;
            shadowTMP.fontSizeMax = mainTMP.fontSizeMax;
            shadowTMP.enableAutoSizing = mainTMP.enableAutoSizing;
            shadowTMP.alignment = mainTMP.alignment;
            shadowTMP.characterSpacing = mainTMP.characterSpacing;
            shadowTMP.wordSpacing = mainTMP.wordSpacing;
            shadowTMP.lineSpacing = mainTMP.lineSpacing;
            shadowTMP.lineSpacingAdjustment = mainTMP.lineSpacingAdjustment;
            shadowTMP.enableWordWrapping = mainTMP.enableWordWrapping;
            shadowTMP.overflowMode = mainTMP.overflowMode;
            shadowTMP.fontStyle = mainTMP.fontStyle;
            shadowTMP.richText = mainTMP.richText;
            shadowTMP.raycastTarget = false;
            shadowTMP.maskable = mainTMP.maskable;
        }

        private static bool TryGetIntermissionTextPair(
            Text source,
            out Text mainSource,
            out Text shadowSource)
        {
            mainSource = null;
            shadowSource = null;

            if (IsIntermissionShadowSource(source))
            {
                shadowSource = source;
                foreach (Transform child in source.transform)
                {
                    Text candidate = child.GetComponent<Text>();
                    if (candidate != null && IsIntermissionShadowChild(candidate))
                    {
                        mainSource = candidate;
                        return true;
                    }
                }
            }
            else if (IsIntermissionShadowChild(source))
            {
                Text candidate = source.transform.parent.GetComponent<Text>();
                if (IsIntermissionShadowSource(candidate))
                {
                    mainSource = source;
                    shadowSource = candidate;
                    return true;
                }
            }

            mainSource = null;
            shadowSource = null;
            return false;
        }

        private static Color GetIntermissionShadowColor(Color sourceColor)
        {
            return new Color(
                0f,
                0f,
                0f,
                Mathf.Min(Mathf.Clamp01(sourceColor.a), IntermissionShadowAlpha));
        }

        private static string StripColorTags(string text)
        {
            return string.IsNullOrEmpty(text)
                ? text
                : Regex.Replace(text, @"</?color(?:\s*=[^>]*)?>", string.Empty, RegexOptions.IgnoreCase);
        }

        private static bool IsIntermissionShadowChild(Text source)
        {
            if (source == null || source.name != "Text (1)" || source.transform.parent == null)
            {
                return false;
            }

            string sceneName = GetCurrentSceneName();
            if (sceneName != "Intermission1" && sceneName != "Intermission2")
            {
                return false;
            }

            return source.transform.parent.name == "Text"
                && source.transform.parent.parent != null
                && source.transform.parent.parent.name == "Panel (1)"
                && source.transform.parent.parent.parent != null
                && source.transform.parent.parent.parent.name == "Panel"
                && source.transform.parent.parent.parent.parent != null
                && source.transform.parent.parent.parent.parent.name == "PowerUpVignette"
                && source.transform.parent.parent.parent.parent.parent != null
                && source.transform.parent.parent.parent.parent.parent.name == "Canvas";
        }

        private static bool IsUniverseLibCanvas(Text source)
        {
            if (source == null)
            {
                return false;
            }

            Transform current = source.transform;
            while (current != null)
            {
                string name = current.name;
                for (int i = 0; i < universeLibPatterns.Length; i++)
                    if (name.IndexOf(universeLibPatterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                current = current.parent;
            }

            return false;
        }

        private static TextMeshProUGUI CreateTMPSibling(Text source)
        {
            if (source == null)
            {
                return null;
            }

            Transform parent = source.transform.parent;
            GameObject tmpObject = new GameObject($"TMP_for_{source.gameObject.name}_{source.GetInstanceID()}", typeof(RectTransform));
            tmpObject.transform.SetParent(parent, worldPositionStays: false);
            tmpObject.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            TextMeshProUGUI tmp = tmpObject.AddComponent<TextMeshProUGUI>();
            LayoutElement layoutElement = tmpObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            CopyRectTransform(source.rectTransform, tmp.rectTransform);

            return tmp;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localScale = source.localScale;
            destination.localEulerAngles = source.localEulerAngles;
            destination.offsetMin = source.offsetMin;
            destination.offsetMax = source.offsetMax;
        }

        private static void CopyTextProperties(Text source, TextMeshProUGUI target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.text = source.text;
            target.fontSize = source.fontSize;
            target.color = source.color;
            target.richText = source.supportRichText;
            target.raycastTarget = source.raycastTarget;
            target.maskable = source.maskable;
            target.lineSpacing = source.lineSpacing;
            target.alignment = ConvertAlignment(source.alignment);
            target.enableWordWrapping = (source.horizontalOverflow == HorizontalWrapMode.Wrap);
            target.overflowMode = (source.verticalOverflow == VerticalWrapMode.Overflow) ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
            target.enableAutoSizing = source.resizeTextForBestFit;
            if (source.resizeTextForBestFit)
            {
                target.fontSizeMin = source.resizeTextMinSize;
                target.fontSizeMax = source.resizeTextMaxSize;
            }
            target.fontStyle = ConvertFontStyle(source.fontStyle);
        }

        private static void SyncEffects(Text source, TextMeshProUGUI target, bool skipShadow = false)
        {
            if (source == null || target == null)
            {
                return;
            }

            Shadow sourceShadow = source.GetComponent<Shadow>();
            Shadow targetShadow = target.GetComponent<Shadow>();
            if (sourceShadow != null && !skipShadow)
            {
                if (targetShadow == null || targetShadow.GetType() != typeof(Shadow))
                {
                    if (targetShadow != null)
                    {
                        UnityEngine.Object.Destroy(targetShadow);
                    }
                    targetShadow = target.gameObject.AddComponent<Shadow>();
                }

                CopyShadowSettings(sourceShadow, targetShadow);
            }
            else if (targetShadow != null && targetShadow.GetType() == typeof(Shadow))
            {
                UnityEngine.Object.Destroy(targetShadow);
            }

            Outline sourceOutline = source.GetComponent<Outline>();
            Outline targetOutline = target.GetComponent<Outline>();
            if (sourceOutline != null)
            {
                if (targetOutline == null)
                {
                    targetOutline = target.gameObject.AddComponent<Outline>();
                }

                CopyShadowSettings(sourceOutline, targetOutline);
            }
            else if (targetOutline != null)
            {
                UnityEngine.Object.Destroy(targetOutline);
            }
        }

        private static void CopyShadowSettings(Shadow source, Shadow target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.effectColor = source.effectColor;
            target.effectDistance = source.effectDistance;
            target.useGraphicAlpha = source.useGraphicAlpha;
        }

        private static void AddIntermissionShadow(TextMeshProUGUI tmp)
        {
            if (tmp == null) return;
            TMPShadowTwin.EnsureShadow(tmp, new Vector2(2.25f, -2.25f));
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.TopLeft;
            }
        }

        private static FontStyles ConvertFontStyle(FontStyle fontStyle)
        {
            switch (fontStyle)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

    }
}
