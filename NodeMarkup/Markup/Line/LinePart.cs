﻿using ModsCommon.Utilities;
using NodeMarkup.Tools;
using NodeMarkup.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace NodeMarkup.Manager
{
    public abstract class MarkupLinePart : IToXml, IRender
    {
        public Action OnRuleChanged { private get; set; }

        public PropertyValue<ISupportPoint> From { get; }
        public PropertyValue<ISupportPoint> To { get; }

        public MarkupLine Line { get; }
        public abstract string XmlSection { get; }

        public MarkupLinePart(MarkupLine line, ISupportPoint from = null, ISupportPoint to = null)
        {
            Line = line;
            From = new PropertyValue<ISupportPoint>(RuleChanged, from);
            To = new PropertyValue<ISupportPoint>(RuleChanged, to);
        }

        protected void RuleChanged() => OnRuleChanged?.Invoke();
        public bool GetFromT(out float t) => GetT(From.Value, out t);
        public bool GetToT(out float t) => GetT(To.Value, out t);
        private bool GetT(ISupportPoint partEdge, out float t)
        {
            if (partEdge != null)
                return partEdge.GetT(Line, out t);
            else
            {
                t = -1;
                return false;
            }
        }
        public bool GetTrajectory(out ITrajectory bezier)
        {
            var succes = false;
            succes |= GetFromT(out float from);
            succes |= GetToT(out float to);

            if (succes)
            {
                bezier = Line.Trajectory.Cut(from != -1 ? from : to, to != -1 ? to : from);
                return true;
            }
            else
            {
                bezier = default;
                return false;
            }

        }
        public virtual void Render(RenderManager.CameraInfo cameraInfo, Color? color = null, float? width = null, bool? alphaBlend = null, bool? cut = null)
        {
            if (GetTrajectory(out ITrajectory trajectory))
                trajectory.Render(cameraInfo, color, width, alphaBlend, cut);
        }

        public virtual XElement ToXml()
        {
            var config = new XElement(XmlSection);

            if (From != null)
                config.Add(From.Value.ToXml());
            if (To != null)
                config.Add(To.Value.ToXml());

            return config;
        }
        protected static IEnumerable<ILinePartEdge> GetEdges(XElement config, MarkupLine line, ObjectsMap map)
        {
            foreach (var supportConfig in config.Elements(LinePartEdge.XmlName))
            {
                if (LinePartEdge.FromXml(supportConfig, line, map, out ILinePartEdge edge))
                    yield return edge;
            }
        }
    }

    public class MarkupLineBound : TrajectoryBound
    {
        public MarkupRegularLine Line {get;}
        public MarkupLineBound(MarkupRegularLine line, float size) : base(line.Trajectory, size)
        {
            Line = line;
        }
    }
}
