using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UltrakULL.Harmony_Patches
{
	public static class TMPFontUtils
	{

		private static bool HasProperty(Material material, string propertyName)
		{
			return material != null && material.HasProperty(propertyName);
		}

		public static bool HasUnderlayOffset(Material material)
		{
			return HasProperty(material, "_UnderlayOffset") ||
				(HasProperty(material, "_UnderlayOffsetX") && HasProperty(material, "_UnderlayOffsetY"));
		}

		public static void SetUnderlayOffset(Material material, Vector4 offset)
		{
			if (HasProperty(material, "_UnderlayOffset"))
				material.SetVector("_UnderlayOffset", offset);
			if (HasProperty(material, "_UnderlayOffsetX"))
				material.SetFloat("_UnderlayOffsetX", offset.x);
			if (HasProperty(material, "_UnderlayOffsetY"))
				material.SetFloat("_UnderlayOffsetY", offset.y);
		}

		public static Vector4 GetUnderlayOffset(Material material, Vector4 fallback)
		{
			if (HasProperty(material, "_UnderlayOffset"))
				return material.GetVector("_UnderlayOffset");
			if (HasProperty(material, "_UnderlayOffsetX") && HasProperty(material, "_UnderlayOffsetY"))
				return new Vector4(material.GetFloat("_UnderlayOffsetX"), material.GetFloat("_UnderlayOffsetY"), 0f, 0f);
			return fallback;
		}

		public static bool CopyFontRenderingProperties(Material source, Material destination)
		{
			if (source == null || destination == null)
				return false;

			bool copiedAtlas = false;
			CopyTexture(source, destination, "_MainTex", ref copiedAtlas);
			CopyTexture(source, destination, "_FaceTex", ref copiedAtlas);

			CopyFloat(source, destination, "_GradientScale");
			CopyFloat(source, destination, "_TextureWidth");
			CopyFloat(source, destination, "_TextureHeight");
			CopyFloat(source, destination, "_ScaleRatioA");
			CopyFloat(source, destination, "_ScaleRatioB");
			CopyFloat(source, destination, "_ScaleRatioC");
			return copiedAtlas;
		}

		public static bool CopyUnderlayProperties(Material source, Material destination)
		{
			if (source == null || destination == null)
				return false;

			bool supportsUnderlay = HasUnderlayOffset(destination) && HasProperty(destination, "_UnderlayColor");
			if (!supportsUnderlay)
			{
				destination.DisableKeyword("UNDERLAY_ON");
				return false;
			}

			CopyColor(source, destination, "_UnderlayColor");
			if (HasUnderlayOffset(source))
				SetUnderlayOffset(destination, GetUnderlayOffset(source, Vector4.zero));
			CopyFloat(source, destination, "_UnderlaySoftness");
			CopyFloat(source, destination, "_UnderlayDilate");

			if (source.IsKeywordEnabled("UNDERLAY_ON"))
				destination.EnableKeyword("UNDERLAY_ON");
			else
				destination.DisableKeyword("UNDERLAY_ON");

			return true;
		}

		public static bool ConfigureUnderlay(
			Material material,
			Vector4 color,
			Vector4 offset,
			float softness,
			float dilate)
		{
			if (material == null ||
			    !HasUnderlayOffset(material) ||
			    !HasProperty(material, "_UnderlayColor"))
			{
				if (material != null)
					material.DisableKeyword("UNDERLAY_ON");
				return false;
			}

			Color shadowColor = TMPShadowTwin.NormalizeGeneratedShadowColor(
				new Color(color.x, color.y, color.z, color.w));
			color = new Vector4(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a);
			material.SetVector("_UnderlayColor", color);
			SetUnderlayOffset(material, offset);
			if (HasProperty(material, "_UnderlaySoftness"))
				material.SetFloat("_UnderlaySoftness", softness);
			if (HasProperty(material, "_UnderlayDilate"))
				material.SetFloat("_UnderlayDilate", dilate);
			material.EnableKeyword("UNDERLAY_ON");
			return true;
		}

		public static bool ConfigureUnderlayFromText(
			Material material,
			TextMeshProUGUI text,
			Vector4 color,
			Vector4 offset,
			float softness,
			float dilate)
		{
			Shadow graphicShadow = GetGraphicShadow(text);
			if (graphicShadow != null)
			{
				Color effectColor = TMPShadowTwin.NormalizeGeneratedShadowColor(graphicShadow.effectColor);
				color = new Vector4(effectColor.r, effectColor.g, effectColor.b, effectColor.a);
				offset = new Vector4(graphicShadow.effectDistance.x, graphicShadow.effectDistance.y, 0f, 0f);
			}

			Vector2 adjustedOffset = TMPShadowTwin.AdjustGeneratedShadowOffset(new Vector2(offset.x, offset.y));
			offset.x = adjustedOffset.x;
			return ConfigureUnderlay(material, color, offset, softness, dilate);
		}

		public static void SetGraphicShadowEnabled(TextMeshProUGUI text, bool enabled)
		{
			Shadow graphicShadow = GetGraphicShadow(text);
			if (graphicShadow != null)
				graphicShadow.enabled = enabled;
		}

		public static Vector2 GetGraphicShadowDistance(TextMeshProUGUI text, Vector2 fallback)
		{
			Shadow graphicShadow = GetGraphicShadow(text);
			return graphicShadow != null ? graphicShadow.effectDistance : fallback;
		}

		public static Color GetGraphicShadowColor(TextMeshProUGUI text, Color fallback)
		{
			Shadow graphicShadow = GetGraphicShadow(text);
			return graphicShadow != null ? graphicShadow.effectColor : fallback;
		}

		private static Shadow GetGraphicShadow(TextMeshProUGUI text)
		{
			if (text == null)
				return null;

			foreach (Shadow shadow in text.GetComponents<Shadow>())
				if (shadow != null && shadow.GetType() == typeof(Shadow))
					return shadow;

			return null;
		}

		public static Material CreateOverlayMaterial(TMP_FontAsset fontAsset, Material fallbackOverlay, string materialName)
		{
			Material source = fontAsset != null ? fontAsset.material : null;
			if (source == null)
				source = fallbackOverlay;
			if (source == null)
				return null;

			Material overlay = new Material(fallbackOverlay != null ? fallbackOverlay : source)
			{
				name = materialName,
				renderQueue = fallbackOverlay != null ? fallbackOverlay.renderQueue : source.renderQueue
			};
			CopyFontRenderingProperties(source, overlay);
			if (overlay.HasProperty("_ZTest"))
				overlay.SetFloat("_ZTest", 8f);
			else
				Logging.Warn($"Overlay material '{overlay.name}' has no _ZTest property.");

			return overlay;
		}

		private static void CopyTexture(Material source, Material destination, string propertyName, ref bool copied)
		{
			if (!HasProperty(source, propertyName) || !HasProperty(destination, propertyName))
				return;

			destination.SetTexture(propertyName, source.GetTexture(propertyName));
			copied = true;
		}

		private static void CopyColor(Material source, Material destination, string propertyName)
		{
			if (HasProperty(source, propertyName) && HasProperty(destination, propertyName))
				destination.SetColor(propertyName, source.GetColor(propertyName));
		}

		private static void CopyFloat(Material source, Material destination, string propertyName)
		{
			if (HasProperty(source, propertyName) && HasProperty(destination, propertyName))
				destination.SetFloat(propertyName, source.GetFloat(propertyName));
		}

		private static Material CreateMigratedMaterial(Material sourceMaterial, Material targetMaterial)
		{
			if (targetMaterial == null)
				return sourceMaterial != null ? new Material(sourceMaterial) : null;

			if (sourceMaterial == null)
				return new Material(targetMaterial);

			try
			{
				Material fallbackMaterial = TMP_MaterialManager.GetFallbackMaterial(sourceMaterial, targetMaterial);
				if (fallbackMaterial != null)
					return new Material(fallbackMaterial);
			}
			catch (Exception exception)
			{
				Logging.Warn($"Could not create a TMP fallback material from '{sourceMaterial.name}' to '{targetMaterial.name}': {exception.Message}");
			}

			Material migratedMaterial = new Material(targetMaterial);
			try
			{
				TMP_MaterialManager.CopyMaterialPresetProperties(sourceMaterial, migratedMaterial);
			}
			catch (Exception exception)
			{
				Logging.Warn($"Could not copy TMP material preset properties from '{sourceMaterial.name}' to '{migratedMaterial.name}': {exception.Message}");
				foreach (string propertyName in sourceMaterial.GetTexturePropertyNames())
				{
					if (HasProperty(migratedMaterial, propertyName))
						migratedMaterial.SetTexture(propertyName, sourceMaterial.GetTexture(propertyName));
				}
			}
			return migratedMaterial;
		}

		public static void ApplyUnderlayAndZTest(
			TextMeshProUGUI instance,
			Vector4 underlayColor,
			Vector4 underlayOffset,
			float underlaySoftness,
			float underlayDilate,
			bool preserveExistingUnderlay,
			bool isUnderlaid,
			bool isOverlay,
			bool editOverlayStatus,
			TMP_FontAsset fontAsset,
			Material overlayMat,
			Material normalMat)
		{
			if (instance == null || fontAsset == null)
				return;

			Material previousInstanceMaterial = ((TMP_Text)instance).fontMaterial;
			Material previousSharedMaterial = ((TMP_Text)instance).fontSharedMaterial;
			Material currentMaterial = previousInstanceMaterial ?? previousSharedMaterial;

			Material baseMaterial = editOverlayStatus ? (isOverlay ? overlayMat : normalMat) : (((TMP_Asset)fontAsset)?.material ?? normalMat);
			if (baseMaterial == null && currentMaterial == null)
				return;

			bool needsUniqueMaterial = isUnderlaid || preserveExistingUnderlay || editOverlayStatus;

            if (!needsUniqueMaterial)
            {
                ((TMP_Text)instance).font = fontAsset;
                ((TMP_Text)instance).fontSharedMaterial = fontAsset.material;
                return;
            }

			Material underlaySource = currentMaterial;
			if ((underlaySource == null || !underlaySource.IsKeywordEnabled("UNDERLAY_ON")) &&
			    previousSharedMaterial != null &&
			    previousSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"))
			{
				underlaySource = previousSharedMaterial;
			}
			bool sourceHadUnderlay = underlaySource != null && underlaySource.IsKeywordEnabled("UNDERLAY_ON");
			bool shouldKeepUnderlay = isUnderlaid ||
				(preserveExistingUnderlay && sourceHadUnderlay);

			//Logging.Message($"[TMPFU] ApplyUnderlay: {instanceName}, isUnderlaid={isUnderlaid}, preserveExisting={preserveExistingUnderlay}, sourceHadUnderlay={sourceHadUnderlay}");
			//Logging.Message($"[TMPFU]   currentMat={currentMaterial?.name ?? "NULL"}, baseMat={baseMaterial?.name ?? "NULL"}");
			//Logging.Message($"[TMPFU]   underlayColor=({underlayColor.x:F2},{underlayColor.y:F2},{underlayColor.z:F2},{underlayColor.w:F2}), offset=({underlayOffset.x:F2},{underlayOffset.y:F2}), softness={underlaySoftness}, dilate={underlayDilate}");
			//Logging.Message($"[TMPFU]   source.shader={currentMaterial?.shader?.name ?? "NULL"}, keyword={currentMaterial?.IsKeywordEnabled("UNDERLAY_ON")}");

			((TMP_Text)instance).font = fontAsset;

			Material val = CreateMigratedMaterial(currentMaterial, baseMaterial);
			if (val == null)
				val = new Material(baseMaterial ?? currentMaterial);

			CopyFontRenderingProperties(fontAsset.material, val);

			bool supportsUnderlay = HasUnderlayOffset(val) && HasProperty(val, "_UnderlayColor");
			//Logging.Message($"[TMPFU]   val: shader={val.shader?.name}, keyword={val.IsKeywordEnabled("UNDERLAY_ON")}, supportsUnderlay={supportsUnderlay}");

			if (supportsUnderlay && shouldKeepUnderlay)
			{
				if (!isUnderlaid && preserveExistingUnderlay && sourceHadUnderlay)
					CopyUnderlayProperties(underlaySource, val);
				else
					ConfigureUnderlayFromText(
						val,
						instance,
						underlayColor,
						underlayOffset,
						underlaySoftness,
						underlayDilate);
				SetGraphicShadowEnabled(instance, false);
			}
			else if (supportsUnderlay)
			{
				//Logging.Message($"[TMPFU]   Clearing underlay");
				if (HasProperty(val, "_UnderlayColor"))
					val.SetVector("_UnderlayColor", new Vector4(0f, 0f, 0f, 0f));
				SetUnderlayOffset(val, Vector4.zero);
				if (HasProperty(val, "_UnderlaySoftness"))
					val.SetFloat("_UnderlaySoftness", 0f);
				if (HasProperty(val, "_UnderlayDilate"))
					val.SetFloat("_UnderlayDilate", 0f);
				val.DisableKeyword("UNDERLAY_ON");
			}
			else
			{
				val.DisableKeyword("UNDERLAY_ON");
			}

			if (editOverlayStatus && HasProperty(val, "_ZTest"))
				val.SetFloat("_ZTest", isOverlay ? 8f : 4f);

			((TMP_Text)instance).fontMaterial = val;
			TMPOverlayShadowFallback.Set(
				instance,
				isUnderlaid && !supportsUnderlay,
				new Vector2(underlayOffset.x, underlayOffset.y)
			);
		}
	}
}
