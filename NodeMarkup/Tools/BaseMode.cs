﻿using ModsCommon.Utilities;
using NodeMarkup.Manager;
using NodeMarkup.UI;
using NodeMarkup.UI.Panel;
using NodeMarkup.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NodeMarkup.Tools
{
    public abstract class BaseToolMode : MonoBehaviour
    {
        public abstract ToolModeType Type { get; }
        public virtual bool ShowPanel => true;

        protected NodeMarkupTool Tool => NodeMarkupTool.Instance;
        public Markup Markup => Tool.Markup;
        protected NodeMarkupPanel Panel => NodeMarkupPanel.Instance;

        public BaseToolMode()
        {
            Disable();
        }

        public virtual void Activate(BaseToolMode prevMode)
        {
            enabled = true;
            Reset(prevMode);
        }
        public virtual void Deactivate() => Disable();
        private void Disable() => enabled = false;

        protected virtual void Reset(BaseToolMode prevMode) { }

        public virtual void Update() { }

        public virtual void OnToolUpdate() { }
        public virtual string GetToolInfo() => null;

        public virtual void OnToolGUI(Event e) { }
        public virtual void OnMouseDown(Event e) { }
        public virtual void OnMouseDrag(Event e) { }
        public virtual void OnMouseUp(Event e) => OnPrimaryMouseClicked(e);
        public virtual void OnPrimaryMouseClicked(Event e) { }
        public virtual void OnSecondaryMouseClicked() { }
        public virtual void RenderOverlay(RenderManager.CameraInfo cameraInfo) { }

        protected string GetCreateToolTip<StyleType>(string text)
            where StyleType : Enum
        {
            var modifiers = GetStylesModifier<StyleType>().ToArray();
            return modifiers.Any() ? $"{text}:\n{string.Join("\n", modifiers)}" : text;
        }
        protected IEnumerable<string> GetStylesModifier<StyleType>()
            where StyleType : Enum
        {
            foreach (var style in EnumExtension.GetEnumValues<StyleType>())
            {
                var general = (Style.StyleType)(object)style;
                var modifier = (StyleModifier)NodeMarkupTool.StylesModifier[general].value;
                if (modifier != StyleModifier.NotSet)
                    yield return $"{general.Description()} - {modifier.Description()}";
            }
        }
    }

    public enum ToolModeType
    {
        None = 0,

        Select = 1,
        MakeLine = 2,
        MakeCrosswalk = 4,
        MakeFiller = 8,
        PanelAction = 16,
        PasteEntersOrder = 32,
        EditEntersOrder = 64,
        ApplyIntersectionTemplateOrder = 128,
        PointsOrder = 256,
        DragPoint = 512,

        MakeItem = MakeLine | MakeCrosswalk
    }
}
