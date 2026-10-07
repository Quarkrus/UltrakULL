using TMPro;
using UnityEngine;

namespace UltrakULL.Harmony_Patches
{
	public class TMPShadowTwin : MonoBehaviour
	{
		public static bool Enabled { get; set; } = false;
		public bool IsOverlayShadowActive => overlayShadowEnabled && twin != null && twin.gameObject.activeInHierarchy;

		private TextMeshProUGUI parentText;
		private TextMeshProUGUI twin;
		private RectTransform twinRt;
		private Vector2 offset = new Vector2(1f, -1.5f);
		private RectTransform parentRt;
		private bool overlayShadowEnabled;
		private Color overlayShadowColor = new Color(0f, 0f, 0f, 0.75f);
		private Material twinMaterial;
		private Material sourceMaterial;
		private const float HorizontalOffsetReduction = 0.5f;
		private const float CharacterSpacingReduction = 0.25f;
		private const float OverlayShadowAlpha = 0.65f;

		public static bool IsShadowGraphic(TMP_Text text)
		{
			return text != null && text.GetComponent<TMPShadowTwinGraphic>() != null;
		}

		public static void EnsureShadow(TextMeshProUGUI text, Vector2? offset = null)
		{
			if (!Enabled || text == null)
				return;

			// Check if shadow twin already exists
			TMPShadowTwin existing = text.gameObject.GetComponent<TMPShadowTwin>();
			if (existing != null)
			{
				Vector2 adjustedOffset = AdjustGeneratedShadowOffset(offset.GetValueOrDefault(existing.offset));
				// Update offset if provided
				if (offset.HasValue && (existing.offset - adjustedOffset).sqrMagnitude > 0.001f)
				{
					existing.offset = adjustedOffset;
					Logging.Message($"[SHADOWTWIN] Updated offset for existing shadow twin: ({existing.offset.x:F2},{existing.offset.y:F2})");
				}
				return;
			}

			TMPShadowTwin controller = text.gameObject.AddComponent<TMPShadowTwin>();
			controller.parentText = text;
			if (offset.HasValue)
				controller.offset = AdjustGeneratedShadowOffset(offset.Value);
			controller.CreateTwin();
		}

		public static void SetOverlayShadow(TextMeshProUGUI text, bool enabled, Vector2 shadowOffset, Color shadowColor)
		{
			if (text == null)
				return;

			TMPShadowTwin controller = text.GetComponent<TMPShadowTwin>();
			if (controller == null && enabled)
				controller = text.gameObject.AddComponent<TMPShadowTwin>();
			if (controller == null)
				return;

			controller.parentText = text;
			controller.overlayShadowEnabled = enabled;
			controller.offset = AdjustOverlayShadowOffset(shadowOffset);
			controller.overlayShadowColor = NormalizeOverlayShadowColor(shadowColor);
			if (controller.twin == null)
				controller.CreateTwin();
			if (controller.twin != null && controller.twin.color != controller.overlayShadowColor)
				controller.twin.color = controller.overlayShadowColor;
			controller.RefreshTwinVisibility();
			controller.SyncOverlayMaterial();
		}

		public static Vector2 AdjustGeneratedShadowOffset(Vector2 shadowOffset)
		{
			shadowOffset.x = Mathf.MoveTowards(shadowOffset.x, 0f, HorizontalOffsetReduction);
			return shadowOffset;
		}

		private static Vector2 AdjustOverlayShadowOffset(Vector2 shadowOffset)
		{
			shadowOffset = AdjustGeneratedShadowOffset(shadowOffset);
			shadowOffset.x = Mathf.MoveTowards(shadowOffset.x, 0f, HorizontalOffsetReduction);
			shadowOffset.y = Mathf.MoveTowards(shadowOffset.y, 0f, 0.25f);
			return shadowOffset;
		}

		public static Color NormalizeGeneratedShadowColor(Color color)
		{
			return new Color(0f, 0f, 0f, color.a > 0.001f ? Mathf.Min(color.a, 0.45f) : 0.45f);
		}

		private static Color NormalizeOverlayShadowColor(Color color)
		{
			return new Color(0f, 0f, 0f, color.a > 0.001f ? Mathf.Min(color.a, OverlayShadowAlpha) : OverlayShadowAlpha);
		}

		public static bool IsSourceVisible(TMP_Text text)
		{
			return text != null &&
			       text.isActiveAndEnabled &&
			       text.gameObject.activeInHierarchy &&
			       text.canvasRenderer != null &&
			       text.canvasRenderer.GetAlpha() > 0.001f &&
			       text.color.a > 0.001f &&
			       !string.IsNullOrEmpty(text.text);
		}

		private static void ApplyShadowMaterialColor(Material material, Color color, bool overlay)
		{
			if (material == null)
				return;

			Color shadowColor = overlay
				? new Color(0f, 0f, 0f, 1f)
				: NormalizeGeneratedShadowColor(color);
			if (material.HasProperty("_FaceColor"))
			{
				if (material.GetColor("_FaceColor") != shadowColor)
					material.SetColor("_FaceColor", shadowColor);
			}
			if (material.HasProperty("_Color"))
			{
				if (material.GetColor("_Color") != shadowColor)
					material.SetColor("_Color", shadowColor);
			}
		}

		private void CreateTwin()
		{
			if (parentText == null || parentText.rectTransform == null)
			{
				Logging.Error($"[SHADOWTWIN] CreateTwin: parentText or rectTransform is null");
				return;
			}

			parentRt = parentText.rectTransform;
			Transform parentTransform = parentRt.parent;
			if (parentTransform == null)
			{
				Logging.Error($"[SHADOWTWIN] CreateTwin: parent transform is null");
				return;
			}

			GameObject twinObject = new GameObject("TMP_ShadowTwin_" + parentText.gameObject.name, typeof(RectTransform));
			twinObject.transform.SetParent(parentTransform, false);
			twinObject.AddComponent<TMPShadowTwinGraphic>();
			
			// Set shadow sibling index BEFORE the original text (so it renders behind)
			int originalIndex = parentRt.GetSiblingIndex();
			twinObject.transform.SetSiblingIndex(originalIndex);

			RectTransform twinRt = twinObject.GetComponent<RectTransform>();
			twinRt.anchorMin = parentRt.anchorMin;
			twinRt.anchorMax = parentRt.anchorMax;
			twinRt.pivot = parentRt.pivot;
			twinRt.sizeDelta = parentRt.sizeDelta;
			twinRt.localScale = parentRt.localScale;
			twinRt.localEulerAngles = parentRt.localEulerAngles;
			twinRt.anchoredPosition = parentRt.anchoredPosition + offset;

			this.twinRt = twinRt;

			twin = twinObject.AddComponent<TextMeshProUGUI>();
			twin.text = parentText.text;
			twin.font = parentText.font;
			twin.fontSize = parentText.fontSize;
			overlayShadowColor = overlayShadowEnabled
				? NormalizeOverlayShadowColor(overlayShadowColor)
				: NormalizeGeneratedShadowColor(overlayShadowColor);
			twin.color = overlayShadowColor;
			twin.alignment = parentText.alignment;
			twin.raycastTarget = false;
			twin.maskable = parentText.maskable;
			twin.enableWordWrapping = parentText.enableWordWrapping;
			twin.overflowMode = parentText.overflowMode;
			twin.enableAutoSizing = parentText.enableAutoSizing;
			twin.fontSizeMin = parentText.fontSizeMin;
			twin.fontSizeMax = parentText.fontSizeMax;
			twin.fontStyle = parentText.fontStyle;
			twin.characterSpacing = parentText.characterSpacing - CharacterSpacingReduction;
			
			// Copy the current TMP material so the overlay shadow uses the same
			// font atlas and render state as its source.
			if (parentText.fontMaterial != null)
			{
				twinMaterial = new Material(parentText.fontMaterial);
				ApplyShadowMaterialColor(twinMaterial, overlayShadowColor, overlayShadowEnabled);
				twin.fontMaterial = twinMaterial;
				sourceMaterial = parentText.fontMaterial;
			}
			else if (parentText.fontSharedMaterial != null)
				twin.fontSharedMaterial = parentText.fontSharedMaterial;
			
			twin.ForceMeshUpdate();

			RefreshTwinVisibility();
		}

		private void OnEnable()
		{
			RefreshTwinVisibility();
		}

		private void OnDisable()
		{
			if (twin != null && twin.gameObject != null)
				twin.gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			if (twin != null && twin.gameObject != null)
				Destroy(twin.gameObject);
			if (twinMaterial != null)
				Destroy(twinMaterial);
		}

		private void Update()
		{
			RefreshTwinVisibility();
			if (parentText == null || twin == null || parentRt == null)
			{
				enabled = false;
				return;
			}

			overlayShadowColor = overlayShadowEnabled
				? NormalizeOverlayShadowColor(overlayShadowColor)
				: NormalizeGeneratedShadowColor(overlayShadowColor);
			if (twin.color != overlayShadowColor)
				twin.color = overlayShadowColor;

			if (!Enabled && !overlayShadowEnabled)
				return;

			// Update text content
			if (twin.text != parentText.text)
				twin.text = parentText.text;
			
			// Update font
			if (twin.font != parentText.font)
				twin.font = parentText.font;
			
			// Update font size
			if (twin.fontSize != parentText.fontSize)
				twin.fontSize = parentText.fontSize;
			
			// Update auto-sizing
			if (twin.enableAutoSizing != parentText.enableAutoSizing)
				twin.enableAutoSizing = parentText.enableAutoSizing;
			
			// Update alignment
			if (twin.alignment != parentText.alignment)
				twin.alignment = parentText.alignment;
			
			// Update font style
			if (twin.fontStyle != parentText.fontStyle)
				twin.fontStyle = parentText.fontStyle;
			float desiredCharacterSpacing = parentText.characterSpacing - CharacterSpacingReduction;
			if (!Mathf.Approximately(twin.characterSpacing, desiredCharacterSpacing))
				twin.characterSpacing = desiredCharacterSpacing;
			if (overlayShadowEnabled &&
			    (sourceMaterial != parentText.fontMaterial ||
			     twinMaterial == null ||
			     twin.fontMaterial != twinMaterial))
				SyncOverlayMaterial();
			else if (parentText.fontMaterial != null && sourceMaterial != parentText.fontMaterial)
			{
				if (twinMaterial != null)
					Destroy(twinMaterial);
				twinMaterial = new Material(parentText.fontMaterial);
				sourceMaterial = parentText.fontMaterial;
				ApplyShadowMaterialColor(twinMaterial, overlayShadowColor, overlayShadowEnabled);
				twin.fontMaterial = twinMaterial;
			}
			
			// Update transform
			if (twinRt != null)
			{
				Vector2 desiredPos = parentRt.anchoredPosition + offset;
				if (twinRt.anchoredPosition != desiredPos)
					twinRt.anchoredPosition = desiredPos;
				if (twinRt.sizeDelta != parentRt.sizeDelta)
					twinRt.sizeDelta = parentRt.sizeDelta;
				if (twinRt.anchorMin != parentRt.anchorMin)
					twinRt.anchorMin = parentRt.anchorMin;
				if (twinRt.anchorMax != parentRt.anchorMax)
					twinRt.anchorMax = parentRt.anchorMax;

				if (twinRt.localScale != parentRt.localScale)
					twinRt.localScale = parentRt.localScale;
			}
		}

		private void RefreshTwinVisibility()
		{
			if (twin != null && twin.gameObject != null)
			{
				bool shouldBeActive = (Enabled || overlayShadowEnabled) && IsSourceVisible(parentText);
				if (twin.gameObject.activeSelf != shouldBeActive)
					twin.gameObject.SetActive(shouldBeActive);
				if (shouldBeActive && parentText != null && twin.transform.parent != parentText.transform.parent)
				{
					twin.transform.SetParent(parentText.transform.parent, false);
					twin.transform.SetSiblingIndex(parentText.transform.GetSiblingIndex());
					parentRt = parentText.rectTransform;
					twinRt = twin.rectTransform;
				}
			}
		}

		private void SyncOverlayMaterial()
		{
			if (!overlayShadowEnabled || parentText == null || twin == null)
				return;

			Material currentSource = parentText.fontMaterial;
			if (currentSource == null)
				return;

			if (twinMaterial == null || twinMaterial.shader != currentSource.shader)
			{
				if (twinMaterial != null)
					Destroy(twinMaterial);
				twinMaterial = new Material(currentSource);
				sourceMaterial = null;
			}

			if (sourceMaterial != currentSource)
			{
				twinMaterial.CopyPropertiesFromMaterial(currentSource);
				twinMaterial.shaderKeywords = currentSource.shaderKeywords;
				twinMaterial.renderQueue = currentSource.renderQueue;
				twinMaterial.DisableKeyword("UNDERLAY_ON");
				sourceMaterial = currentSource;
			}

			ApplyShadowMaterialColor(twinMaterial, overlayShadowColor, overlayShadowEnabled);
			if (twin.fontMaterial != twinMaterial)
				twin.fontMaterial = twinMaterial;
		}
	}

	public sealed class TMPShadowTwinGraphic : MonoBehaviour
	{
	}
}