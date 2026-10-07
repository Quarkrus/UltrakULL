using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UltrakULL.Harmony_Patches
{
	public static class TMPShadowPolicy
	{
		public static bool RequiresForcedShadow(Transform target)
		{
			if (target == null)
				return false;

			TextMeshProUGUI convertedTMP = target.GetComponent<TextMeshProUGUI>();
			Text sourceText = convertedTMP != null
				? TextToTMPConverter.GetSourceText(convertedTMP)
				: null;
			if (sourceText != null && RequiresForcedShadow(sourceText.transform))
				return true;

			if (target.GetComponent<Shadow>() != null)
				return true;

			if (ContainsName(target.name, "NameText") || ContainsName(target.name, "LayerText"))
				return true;

			Transform current = target;
			while (current != null)
			{
				if (ContainsName(current.name, "Cheats Info"))
					return true;
				current = current.parent;
			}

			return HasReadingScannedPanelTextPath(target) ||
			       HasNavmeshWarningTextPath(target) ||
			       IsCutsceneSkipText(target) ||
			       IsClassicHudHealthTitle(target) ||
			       IsStandardHudHealthSymbol(target);
		}

		private static bool IsStandardHudHealthSymbol(Transform target)
		{
			if (!ContainsName(target.name, "HP Symbol") && !ContainsName(target.name, "Plus"))
				return false;

			for (Transform current = target.parent; current != null; current = current.parent)
				if (current.GetComponent<HealthBar>() != null)
					return true;

			return false;
		}

		private static bool IsCutsceneSkipText(Transform target)
		{
			return target.name.Equals("CutsceneSkipText", StringComparison.OrdinalIgnoreCase) &&
			       target.parent != null &&
			       target.parent.name.Equals("Canvas", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsClassicHudHealthTitle(Transform target)
		{
			if (target == null ||
			    target.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) < 0 ||
			    target.parent == null ||
			    target.parent.name.IndexOf("Health", StringComparison.OrdinalIgnoreCase) < 0)
				return false;

			for (Transform current = target.parent.parent; current != null; current = current.parent)
				if (current.name.IndexOf("AltHud", StringComparison.OrdinalIgnoreCase) >= 0)
					return true;

			return false;
		}

		public static bool IsAltHud(TMP_Text text)
		{
			Transform current = text != null ? text.transform : null;
			while (current != null)
			{
				if (current.name.IndexOf("AltHud", StringComparison.OrdinalIgnoreCase) >= 0)
					return current.name.IndexOf(" (2)", StringComparison.OrdinalIgnoreCase) >= 0;
				current = current.parent;
			}

			return false;
		}

		private static bool HasReadingScannedPanelTextPath(Transform target)
		{
			return target.name == "Text (1)" &&
			       target.parent != null &&
			       target.parent.name == "Panel" &&
			       target.parent.parent != null &&
			       target.parent.parent.name == "ReadingScanned";
		}

		private static bool HasNavmeshWarningTextPath(Transform target)
		{
			return target.name == "Text (1)" &&
			       target.parent != null &&
			       target.parent.name == "Navmesh Warning" &&
			       target.parent.parent != null &&
			       target.parent.parent.name == "Canvas";
		}

		private static bool ContainsName(string value, string expected)
		{
			return !string.IsNullOrEmpty(value) &&
			       value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
