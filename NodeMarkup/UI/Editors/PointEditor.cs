﻿using ColossalFramework.UI;
using ModsCommon.UI;
using NodeMarkup.Manager;
using NodeMarkup.Tools;
using NodeMarkup.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NodeMarkup.UI.Editors
{
    public class PointsEditor : Editor<PointItem, MarkupPoint, ColorIcon>
    {
        protected override bool UseGroupPanel => true;

        public override string Name => NodeMarkup.Localize.PointEditor_Points;
        public override string EmptyMessage => string.Empty;
        public override Type SupportType { get; } = typeof(ISupportPoints);

        private FloatPropertyPanel Offset { get; set; }
        protected override void FillItems()
        {
            foreach (var enter in Markup.Enters)
            {
                foreach (var point in enter.Points)
                    AddItem(point);
            }
        }
        protected override void OnObjectSelect()
        {
            Offset = ComponentPool.Get<FloatPropertyPanel>(PropertiesPanel);
            Offset.Text = NodeMarkup.Localize.PointEditor_Offset;
            Offset.UseWheel = true;
            Offset.WheelStep = 0.1f;
            Offset.Init();
            Offset.Value = EditObject.Offset;
            Offset.OnValueChanged += OffsetChanged;
        }
        protected override void OnClear()
        {
            Offset = null;
        }
        protected override void OnObjectUpdate()
        {
            Offset.OnValueChanged -= OffsetChanged;
            Offset.Value = EditObject.Offset;
            Offset.OnValueChanged += OffsetChanged;
        }
        private void OffsetChanged(float value) => EditObject.Offset = value;

        public override void Render(RenderManager.CameraInfo cameraInfo)
        {
            if (IsHoverItem)
                HoverItem.Object.Render(cameraInfo, Colors.White, 2f);
        }
    }
    public class PointItem : EditableItem<MarkupPoint, ColorIcon>
    {
        public override bool ShowDelete => false;
        public override void Init(MarkupPoint editableObject)
        {
            base.Init(editableObject);
            Icon.InnerColor = Object.Color;
        }
    }
}
