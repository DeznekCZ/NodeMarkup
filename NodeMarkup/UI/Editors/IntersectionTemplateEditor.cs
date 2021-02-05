﻿using ColossalFramework.UI;
using ModsCommon.UI;
using NodeMarkup.Manager;
using NodeMarkup.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NodeMarkup.UI.Editors
{
    public class IntersectionTemplateEditor : BaseTemplateEditor<IntersectionTemplateItem, IntersectionTemplate, IntersectionTemplateIcon, IntersectionTemplateGroup, bool, IntersectionTemplateHeaderPanel, EditIntersectionTemplateMode>
    {
        public override string Name => NodeMarkup.Localize.PresetEditor_Presets;
        public override string EmptyMessage => string.Format(NodeMarkup.Localize.PresetEditor_EmptyMessage, NodeMarkup.Localize.Panel_SaveAsPreset);
        public override Type SupportType { get; } = typeof(ISupportIntersectionTemplate);
        protected override string IsAssetMessage => NodeMarkup.Localize.PresetEditor_PresetIsAsset;
        protected override string RewriteCaption => NodeMarkup.Localize.PresetEditor_RewriteCaption;
        protected override string RewriteMessage => NodeMarkup.Localize.PresetEditor_RewriteMessage;
        protected override string SaveChangesMessage => NodeMarkup.Localize.PresetEditor_SaveChangesMessage;
        protected override string NameExistMessage => NodeMarkup.Localize.PresetEditor_NameExistMessage;
        protected override string IsAssetWarningMessage => NodeMarkup.Localize.PresetEditor_IsAssetWarningMessage;
        protected override string IsWorkshopWarningMessage => NodeMarkup.Localize.PresetEditor_IsWorkshopWarningMessage;

        protected override bool GroupingEnabled => false;

        protected override IEnumerable<IntersectionTemplate> GetTemplates() => TemplateManager.IntersectionManager.Templates.OrderBy(t => t.Roads).ThenBy(t => t.Name);
        protected override bool SelectGroup(IntersectionTemplate editableItem) => true;
        protected override string GroupName(bool group) => throw new NotSupportedException();

        protected override void ClearSettings()
        {
            RemovePreview();
            base.ClearSettings();
        }
        protected override void AddHeader()
        {
            base.AddHeader();
            HeaderPanel.OnApply += OnApply;
        }
        protected override void AddAdditional() => AddScreenshot();
        private void AddScreenshot()
        {
            var group = ComponentPool.Get<PropertyGroupPanel>(ContentPanel);
            var info = ComponentPool.Get<IntersectionTemplateInfoProperty>(group);
            info.Init(EditObject);
        }
        private void OnApply() => Tool.ApplyIntersectionTemplate(EditObject);
        protected override void OnObjectDelete(IntersectionTemplate template)
        {
            base.OnObjectDelete(template);
            RemovePreview();
        }

        PropertyGroupPanel Preview { get; set; }
        protected override void ItemHover(IntersectionTemplateItem editableItem)
        {
            base.ItemHover(editableItem);
            AddPreview(editableItem);
        }
        protected override void ItemLeave()
        {
            base.ItemLeave();
            RemovePreview();
        }
        private void AddPreview(IntersectionTemplateItem editableItem)
        {
            if (HoverItem == SelectItem)
                return;

            ContentPanel.opacity = 0.15f;

            var root = GetRootContainer();
            Preview = ComponentPool.Get<PreviewPanel>(root);
            var info = ComponentPool.Get<PreviewIntersectionTemplateInfo>(Preview);
            info.Init(HoverItem.Object);
            Preview.width = 365f;

            var x = editableItem.absolutePosition.x + editableItem.width;
            var y = Mathf.Min(editableItem.absolutePosition.y, root.absolutePosition.y + root.height - Preview.height);
            Preview.absolutePosition = new Vector2(x, y);
        }
        private void RemovePreview()
        {
            if (Preview == null)
                return;

            ContentPanel.opacity = 1f;
            ComponentPool.Free(Preview);
            Preview = null;
        }
    }

    public class IntersectionTemplateItem : EditableItem<IntersectionTemplate, IntersectionTemplateIcon>
    {
        public override bool ShowDelete => !Object.IsAsset;

        public override void Refresh()
        {
            base.Refresh();
            Icon.Count = Object.Roads;
        }
    }
    public class IntersectionTemplateIcon : ColorIcon
    {
        protected UILabel CountLabel { get; }
        public int Count { set => CountLabel.text = value.ToString(); }
        public IntersectionTemplateIcon()
        {
            CountLabel = AddUIComponent<UILabel>();
            CountLabel.textColor = Color.white;
            CountLabel.textScale = 0.7f;
            CountLabel.relativePosition = new Vector3(0, 0);
            CountLabel.autoSize = false;
            CountLabel.textAlignment = UIHorizontalAlignment.Center;
            CountLabel.verticalAlignment = UIVerticalAlignment.Middle;
            CountLabel.padding = new RectOffset(0, 0, 5, 0);
        }
        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            if (CountLabel != null)
                CountLabel.size = size;
        }
    }
    public class IntersectionTemplateGroup : EditableGroup<bool, IntersectionTemplateItem, IntersectionTemplate, IntersectionTemplateIcon> { }
    public class EditIntersectionTemplateMode : EditTemplateMode<IntersectionTemplate> { }
    public class PreviewPanel : PropertyGroupPanel
    {
        protected override Color32 Color => new Color32(201, 211, 216, 255);

        protected override void OnTooltipEnter(UIMouseEventParameter p) { return; }
        protected override void OnTooltipHover(UIMouseEventParameter p) { return; }
        protected override void OnTooltipLeave(UIMouseEventParameter p) { return; }
    }
    public class PreviewIntersectionTemplateInfo : IntersectionTemplateInfoProperty
    {
        protected override void OnTooltipEnter(UIMouseEventParameter p) { return; }
        protected override void OnTooltipHover(UIMouseEventParameter p) { return; }
        protected override void OnTooltipLeave(UIMouseEventParameter p) { return; }
    }
}
