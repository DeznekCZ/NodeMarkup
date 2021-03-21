﻿using ModsCommon.Utilities;
using NodeMarkup.Tools;
using NodeMarkup.UI.Editors;
using NodeMarkup.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace NodeMarkup.Manager
{
    public class MarkupCrosswalk : IStyleItem, IToXml
    {
        #region PROPERTIES

        public static string XmlName { get; } = "C";
        public string XmlSection => XmlName;

        public string DeleteCaptionDescription => Localize.CrossWalkEditor_DeleteCaptionDescription;
        public string DeleteMessageDescription => Localize.CrossWalkEditor_DeleteMessageDescription;

        public Markup Markup { get; }
        public MarkupCrosswalkLine CrosswalkLine { get; }

        public LodDictionary<IStyleData> StyleData { get; } = new LodDictionary<IStyleData>();
        public MarkupEnterLine EnterLine { get; private set; }

        public PropertyValue<MarkupRegularLine> RightBorder { get; }
        public PropertyValue<MarkupRegularLine> LeftBorder { get; }
        public PropertyValue<CrosswalkStyle> Style { get; }

        private StraightTrajectory DefaultRightBorderTrajectory => new StraightTrajectory(EnterLine.Start.Position, EnterLine.Start.Position + NormalDir * TotalWidth);
        private StraightTrajectory DefaultLeftBorderTrajectory => new StraightTrajectory(EnterLine.End.Position, EnterLine.End.Position + NormalDir * TotalWidth);
        public ITrajectory RightBorderTrajectory { get; private set; }
        public ITrajectory LeftBorderTrajectory { get; private set; }

        public ITrajectory[] BorderTrajectories => new ITrajectory[] { EnterLine.Trajectory, CrosswalkLine.Trajectory, RightBorderTrajectory, LeftBorderTrajectory };

        public float TotalWidth => Style.Value.GetTotalWidth(this);
        public float CornerAndNormalAngle => EnterLine.Start.Enter.CornerAndNormalAngle;
        public Vector3 NormalDir => EnterLine.Start.Enter.NormalDir;
        public Vector3 CornerDir => EnterLine.Start.Enter.CornerDir;

        #endregion

        public MarkupCrosswalk(Markup markup, MarkupCrosswalkLine crosswalkLine, CrosswalkStyle.CrosswalkType crosswalkType = CrosswalkStyle.CrosswalkType.Existent) :
            this(markup, crosswalkLine, TemplateManager.StyleManager.GetDefault<CrosswalkStyle>((Style.StyleType)(int)crosswalkType))
        { }
        public MarkupCrosswalk(Markup markup, MarkupCrosswalkLine line, CrosswalkStyle style, MarkupRegularLine rightBorder = null, MarkupRegularLine leftBorder = null)
        {
            Markup = markup;
            CrosswalkLine = line;
            CrosswalkLine.TrajectoryGetter = GetTrajectory;

            RightBorder = new PropertyValue<MarkupRegularLine>("RB", CrosswalkChanged, rightBorder);
            LeftBorder = new PropertyValue<MarkupRegularLine>("LB", CrosswalkChanged, leftBorder);
            style.OnStyleChanged = CrosswalkChanged;
            Style = new PropertyValue<CrosswalkStyle>(StyleChanged, style);

            CrosswalkLine.Start.Enter.TryGetPoint(CrosswalkLine.Start.Num, MarkupPoint.PointType.Enter, out MarkupPoint startPoint);
            CrosswalkLine.End.Enter.TryGetPoint(CrosswalkLine.End.Num, MarkupPoint.PointType.Enter, out MarkupPoint endPoint);
            EnterLine = new MarkupEnterLine(Markup, startPoint, endPoint);
        }
        private void StyleChanged()
        {
            Style.Value.OnStyleChanged = CrosswalkChanged;
            CrosswalkChanged();
        }
        protected void CrosswalkChanged() => Markup.Update(this, true, true);

        public void Update(bool onlySelfUpdate = false)
        {
            EnterLine.Update(GetAlignment(EnterLine.Start, RightBorder), GetAlignment(EnterLine.End, LeftBorder), true);
            CrosswalkLine.Update(true);

            if (!onlySelfUpdate)
                Markup.Update(this);

            static LineAlignment GetAlignment(MarkupPoint point, MarkupRegularLine border)
            {
                if (border is MarkupRegularLine line)
                    return line.Start == point ? line.Alignment : line.Alignment.Value.Invert();
                else
                    return LineAlignment.Centre;
            }
        }

        public void RecalculateStyleData()
        {
#if DEBUG_RECALCULATE
            Mod.Logger.Debug($"Recalculate crosswalk {this}");
#endif
            foreach (var lod in EnumExtension.GetEnumValues<MarkupLOD>())
                RecalculateStyleData(lod);
        }
        public void RecalculateStyleData(MarkupLOD lod) => StyleData[lod] = new MarkupStyleParts(Style.Value.Calculate(this, lod));

        public MarkupRegularLine GetBorder(BorderPosition borderType) => borderType == BorderPosition.Right ? RightBorder : LeftBorder;

        private StraightTrajectory GetOffsetTrajectory(float offset)
        {
            var start = EnterLine.Start.Position + NormalDir * offset;
            var end = EnterLine.End.Position + NormalDir * offset;
            return new StraightTrajectory(start, end, false);
        }
        private StraightTrajectory GetTrajectory()
        {
            var trajectory = GetOffsetTrajectory(TotalWidth);

            RightBorderTrajectory = GetBorderTrajectory(trajectory, RightBorder, 0, DefaultRightBorderTrajectory, out float startT);
            LeftBorderTrajectory = GetBorderTrajectory(trajectory, LeftBorder, 1, DefaultLeftBorderTrajectory, out float endT);

            return (StraightTrajectory)trajectory.Cut(startT, endT);
        }
        private ITrajectory GetBorderTrajectory(StraightTrajectory trajectory, MarkupLine border, float defaultT, StraightTrajectory defaultTrajectory, out float t)
        {
            if (border != null && MarkupIntersect.CalculateSingle(trajectory, border.Trajectory) is MarkupIntersect intersect && intersect.IsIntersect)
            {
                t = intersect.FirstT;
                return EnterLine.PointPair.ContainPoint(border.Start) ? border.Trajectory.Cut(0, intersect.SecondT) : border.Trajectory.Cut(intersect.SecondT, 1);
            }
            else
            {
                t = defaultT;
                return defaultTrajectory;
            }
        }

        public StraightTrajectory GetTrajectory(float offset)
        {
            var trajectory = GetOffsetTrajectory(offset);

            var startT = GetT(trajectory, RightBorderTrajectory, 0);
            var endT = GetT(trajectory, LeftBorderTrajectory, 1);

            return (StraightTrajectory)trajectory.Cut(startT, endT);

            static float GetT(StraightTrajectory trajectory, ITrajectory lineTrajectory, float defaultT)
            => MarkupIntersect.CalculateSingle(trajectory, lineTrajectory) is MarkupIntersect intersect && intersect.IsIntersect ? intersect.FirstT : defaultT;
        }
        public StraightTrajectory GetFullTrajectory(float offset, Vector3 normal)
        {
            var trajectory = GetOffsetTrajectory(offset);

            var startT = GetT(trajectory, normal, new Vector3[] { EnterLine.Start.Position, CrosswalkLine.Trajectory.StartPosition }, 0, MinAggregate);
            var endT = GetT(trajectory, normal, new Vector3[] { EnterLine.End.Position, CrosswalkLine.Trajectory.EndPosition }, 1, MaxAggregate);

            return (StraightTrajectory)trajectory.Cut(startT, endT);

            static float MinAggregate(MarkupIntersect[] intersects) => intersects.Min(i => i.IsIntersect ? i.FirstT : 0);
            static float MaxAggregate(MarkupIntersect[] intersects) => intersects.Max(i => i.IsIntersect ? i.FirstT : 1);
            static float GetT(StraightTrajectory trajectory, Vector3 normal, Vector3[] positions, float defaultT, Func<MarkupIntersect[], float> aggregate)
            {
                var intersects = positions.SelectMany(p => MarkupIntersect.Calculate(trajectory, new StraightTrajectory(p, p + normal, false))).ToArray();
                return intersects.Any() ? aggregate(intersects) : defaultT;
            }
        }

        public bool IsBorder(MarkupLine line) => line != null && (line == RightBorder.Value || line == LeftBorder.Value);
        public void RemoveBorder(MarkupLine line)
        {
            if (line == RightBorder.Value)
                RightBorder.Value = null;

            if (line == LeftBorder.Value)
                LeftBorder.Value = null;
        }
        public bool ContainsPoint(MarkupPoint point) => EnterLine.ContainsPoint(point);

        public Dependences GetDependences() => Markup.GetCrosswalkDependences(this);
        public void Render(RenderManager.CameraInfo cameraInfo, Color? color = null, float? width = null, bool? alphaBlend = null, bool? cut = null)
        {
            foreach (var trajectory in BorderTrajectories)
                trajectory.Render(cameraInfo, color, width, alphaBlend, cut);
        }

        #region XML

        public XElement ToXml()
        {
            var config = new XElement(XmlName);
            config.Add(new XAttribute(MarkupLine.XmlName, CrosswalkLine.PointPair.Hash));
            if (RightBorder.Value != null)
                config.Add(new XAttribute("RB", RightBorder.Value.PointPair.Hash));
            if (LeftBorder.Value != null)
                config.Add(new XAttribute("LB", LeftBorder.Value.PointPair.Hash));
            config.Add(Style.Value.ToXml());
            return config;
        }
        public void FromXml(XElement config, ObjectsMap map)
        {
            RightBorder.Value = GetBorder(map.IsMirror ? "LB" : "RB");
            LeftBorder.Value = GetBorder(map.IsMirror ? "RB" : "LB");
            if (config.Element(Manager.Style.XmlName) is XElement styleConfig && Manager.Style.FromXml(styleConfig, map, false, out CrosswalkStyle style))
                Style.Value = style;

            MarkupRegularLine GetBorder(string key)
            {
                var lineId = config.GetAttrValue<ulong>(key);
                return Markup.TryGetLine(lineId, map, out MarkupRegularLine line) ? line : null;
            }
        }

        public static bool FromXml(XElement config, Markup markup, ObjectsMap map, out MarkupCrosswalk crosswalk)
        {
            var lineId = config.GetAttrValue<ulong>(MarkupLine.XmlName);
            if (markup.TryGetLine(lineId, map, out MarkupCrosswalkLine line))
            {
                crosswalk = line.Crosswalk;
                crosswalk.FromXml(config, map);
                return true;
            }
            else
            {
                crosswalk = null;
                return false;
            }
        }

        #endregion

        public override string ToString() => CrosswalkLine.ToString();
    }
}
