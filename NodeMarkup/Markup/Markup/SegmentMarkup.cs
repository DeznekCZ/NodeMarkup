﻿using IMT.Utils;
using ModsCommon.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace IMT.Manager
{
    public class SegmentMarkup : Markup<NodeEnter>
    {
        public static string XmlName { get; } = "S";

        public override string XmlSection => XmlName;

        public SegmentMarkup(ushort nodeId) : base(nodeId) { }

        protected override Vector3 GetPosition() => Id.GetSegment().m_middlePosition;
        protected override IEnumerable<ushort> GetEnters() => Id.GetSegment().NodesID();
        protected override Enter NewEnter(ushort id) => new SegmentEnter(this, id);


        public static bool FromXml(Version version, XElement config, ObjectsMap map, out SegmentMarkup markup)
        {
            var segmentId = config.GetAttrValue<ushort>(nameof(Id));
            while (map.TryGetValue(new ObjectId() { Segment = segmentId }, out ObjectId targetSegment))
                segmentId = targetSegment.Node;

            try
            {
                markup = MarkupManager.SegmentManager.Get(segmentId);
                markup.FromXml(version, config, map);
                return true;
            }
            catch (Exception error)
            {
                Mod.Logger.Error($"Could not load segment #{segmentId} markup", error);
                markup = null;
                MarkupManager.LoadErrors += 1;
                return false;
            }
        }
    }
}
