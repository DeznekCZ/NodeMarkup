﻿using ColossalFramework.Math;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using ModsCommon.UI;
using ModsCommon.Utilities;
using NodeMarkup.UI;
using NodeMarkup.UI.Editors;
using NodeMarkup.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace NodeMarkup.Manager
{
    public interface IFillerStyle : IStyle
    {
        PropertyValue<float> MedianOffset { get;}
    }
    public abstract class FillerStyle : Style, IFillerStyle
    {
        public static float DefaultAngle => 0f;
        public static float DefaultStepStripe => 3f;
        public static float DefaultStepGrid => 6f;
        public static float DefaultOffset => 0f;
        public static float StripeDefaultWidth => 0.5f;
        public static float DefaultAngleBetween => 90f;
        public static float DefaultElevation => 0.3f;
        public static bool DefaultFollowLines => false;

        static Dictionary<FillerType, FillerStyle> Defaults { get; } = new Dictionary<FillerType, FillerStyle>()
        {
            {FillerType.Stripe, new StripeFillerStyle(DefaultColor, StripeDefaultWidth, DefaultAngle, DefaultStepStripe, DefaultOffset, DefaultOffset, DefaultFollowLines, 0, 1, 1, 2)},
            {FillerType.Grid, new GridFillerStyle(DefaultColor, DefaultWidth, DefaultAngle, DefaultStepGrid, DefaultOffset, DefaultOffset)},
            {FillerType.Solid, new SolidFillerStyle(DefaultColor, DefaultOffset)},
            {FillerType.Chevron, new ChevronFillerStyle(DefaultColor, StripeDefaultWidth, DefaultOffset, DefaultAngleBetween, DefaultStepStripe)},
            {FillerType.Pavement, new PavementFillerStyle(DefaultColor, DefaultWidth, DefaultOffset, DefaultElevation)},
            {FillerType.Grass, new GrassFillerStyle(DefaultColor, DefaultWidth, DefaultOffset, DefaultElevation)},
        };

        public static FillerStyle GetDefault(FillerType type) => Defaults.TryGetValue(type, out FillerStyle style) ? style.CopyFillerStyle() : null;

        public PropertyValue<float> MedianOffset { get; }

        public FillerStyle(Color32 color, float width, float medianOffset) : base(color, width)
        {
            MedianOffset = GetMedianOffsetProperty(medianOffset);
        }

        public override void CopyTo(Style target)
        {
            base.CopyTo(target);
            if (target is IFillerStyle fillerTarget)
                fillerTarget.MedianOffset.Value = MedianOffset;
        }

        public sealed override List<EditorItem> GetUIComponents(object editObject, UIComponent parent, Action onHover = null, Action onLeave = null, bool isTemplate = false)
        {
            var components = base.GetUIComponents(editObject, parent, onHover, onLeave, isTemplate);
            if (editObject is MarkupFiller filler)
                GetUIComponents(filler, components, parent, onHover, onLeave, isTemplate);
            else if (isTemplate)
                GetUIComponents(null, components, parent, onHover, onLeave, isTemplate);
            return components;
        }
        public virtual void GetUIComponents(MarkupFiller filler, List<EditorItem> components, UIComponent parent, Action onHover = null, Action onLeave = null, bool isTemplate = false)
        {
            if (!isTemplate && filler.IsMedian)
                components.Add(AddMedianOffsetProperty(this, parent, onHover, onLeave));
        }

        public sealed override Style Copy() => CopyFillerStyle();
        public abstract FillerStyle CopyFillerStyle();
        public abstract IStyleData Calculate(MarkupFiller filler, MarkupLOD lod);

        public ITrajectory[] SetMedianOffset(MarkupFiller filler)
        {
            var lineParts = filler.Contour.Parts.ToArray();
            var trajectories = filler.Contour.TrajectoriesRaw.ToArray();

            for (var i = 0; i < lineParts.Length; i += 1)
            {
                if (trajectories[i] == null)
                    continue;

                var line = lineParts[i].Line;
                if (line is MarkupEnterLine)
                    continue;

                var prevI = i == 0 ? lineParts.Length - 1 : i - 1;
                if (lineParts[prevI].Line is MarkupEnterLine && trajectories[prevI] != null)
                {
                    trajectories[i] = Shift(trajectories[i]);
                    trajectories[prevI] = new StraightTrajectory(trajectories[prevI].StartPosition, trajectories[i].StartPosition);
                }

                var nextI = i + 1 == lineParts.Length ? 0 : i + 1;
                if (lineParts[nextI].Line is MarkupEnterLine && trajectories[nextI] != null)
                {
                    trajectories[i] = Shift(trajectories[i].Invert()).Invert();
                    trajectories[nextI] = new StraightTrajectory(trajectories[i].EndPosition, trajectories[nextI].EndPosition);
                }

                ITrajectory Shift(ITrajectory trajectory)
                {
                    var newT = trajectory.Travel(0, MedianOffset);
                    return trajectory.Cut(newT, 1);
                }
            }

            return trajectories.Where(t => t != null).Select(t => t).ToArray();
        }
        protected float GetOffset(MarkupIntersect intersect, float offset)
        {
            var sin = Mathf.Sin(intersect.Angle);
            return sin != 0 ? offset / sin : 1000f;
        }

        public override XElement ToXml()
        {
            var config = base.ToXml();
            config.Add(MedianOffset.ToXml());
            return config;
        }
        public override void FromXml(XElement config, ObjectsMap map, bool invert)
        {
            base.FromXml(config, map, invert);
            MedianOffset.FromXml(config, DefaultOffset);
        }

        protected static FloatPropertyPanel AddMedianOffsetProperty(FillerStyle fillerStyle, UIComponent parent, Action onHover, Action onLeave)
        {
            var offsetProperty = ComponentPool.Get<FloatPropertyPanel>(parent);
            offsetProperty.Text = Localize.StyleOption_MedianOffset;
            offsetProperty.UseWheel = true;
            offsetProperty.WheelStep = 0.1f;
            offsetProperty.WheelTip = Editor.WheelTip;
            offsetProperty.CheckMin = true;
            offsetProperty.MinValue = 0f;
            offsetProperty.Init();
            offsetProperty.Value = fillerStyle.MedianOffset;
            offsetProperty.OnValueChanged += (float value) => fillerStyle.MedianOffset.Value = value;
            AddOnHoverLeave(offsetProperty, onHover, onLeave);
            return offsetProperty;
        }
        protected static FloatPropertyPanel AddAngleProperty(IRotateFiller rotateStyle, UIComponent parent, Action onHover, Action onLeave)
        {
            var angleProperty = ComponentPool.Get<FloatPropertyPanel>(parent);
            angleProperty.Text = Localize.StyleOption_Angle;
            angleProperty.UseWheel = true;
            angleProperty.WheelStep = 1f;
            angleProperty.WheelTip = Editor.WheelTip;
            angleProperty.CheckMin = true;
            angleProperty.MinValue = -90;
            angleProperty.CheckMax = true;
            angleProperty.MaxValue = 90;
            angleProperty.CyclicalValue = true;
            angleProperty.Init();
            angleProperty.Value = rotateStyle.Angle;
            angleProperty.OnValueChanged += (float value) => rotateStyle.Angle.Value = value;
            AddOnHoverLeave(angleProperty, onHover, onLeave);
            return angleProperty;
        }
        protected static FloatPropertyPanel AddStepProperty(IPeriodicFiller periodicStyle, UIComponent parent, Action onHover, Action onLeave)
        {
            var stepProperty = ComponentPool.Get<FloatPropertyPanel>(parent);
            stepProperty.Text = Localize.StyleOption_Step;
            stepProperty.UseWheel = true;
            stepProperty.WheelStep = 0.1f;
            stepProperty.WheelTip = Editor.WheelTip;
            stepProperty.CheckMin = true;
            stepProperty.MinValue = 1.5f;
            stepProperty.Init();
            stepProperty.Value = periodicStyle.Step;
            stepProperty.OnValueChanged += (float value) => periodicStyle.Step.Value = value;
            AddOnHoverLeave(stepProperty, onHover, onLeave);
            return stepProperty;
        }
        protected static FloatPropertyPanel AddOffsetProperty(IOffsetFiller offsetStyle, UIComponent parent, Action onHover, Action onLeave)
        {
            var offsetProperty = ComponentPool.Get<FloatPropertyPanel>(parent);
            offsetProperty.Text = Localize.StyleOption_Offset;
            offsetProperty.UseWheel = true;
            offsetProperty.WheelStep = 0.1f;
            offsetProperty.WheelTip = Editor.WheelTip;
            offsetProperty.CheckMin = true;
            offsetProperty.MinValue = 0f;
            offsetProperty.Init();
            offsetProperty.Value = offsetStyle.Offset;
            offsetProperty.OnValueChanged += (float value) => offsetStyle.Offset.Value = value;
            AddOnHoverLeave(offsetProperty, onHover, onLeave);
            return offsetProperty;
        }

        public enum FillerType
        {
            [Description(nameof(Localize.FillerStyle_Stripe))]
            Stripe = StyleType.FillerStripe,

            [Description(nameof(Localize.FillerStyle_Grid))]
            Grid = StyleType.FillerGrid,

            [Description(nameof(Localize.FillerStyle_Solid))]
            Solid = StyleType.FillerSolid,

            [Description(nameof(Localize.FillerStyle_Chevron))]
            Chevron = StyleType.FillerChevron,

            [Description(nameof(Localize.FillerStyle_Pavement))]
            Pavement = StyleType.FillerPavement,

            [Description(nameof(Localize.FillerStyle_Grass))]
            Grass = StyleType.FillerGrass,
        }
    }
}
