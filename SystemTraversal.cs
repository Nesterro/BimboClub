using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace AntigravityScheme
{
    public class TraversalNode
    {
        public Element Element { get; set; }
        public XYZ Location { get; set; }
        public List<TraversalNode> ConnectedNodes { get; set; } = new List<TraversalNode>();
    }

    public class SystemTraversal
    {
        private Document _doc;
        private HashSet<int> _visitedElementIds;

        public SystemTraversal(Document doc)
        {
            _doc = doc;
            _visitedElementIds = new HashSet<int>();
        }

        public List<TraversalNode> Traverse(Element startElement)
        {
            List<TraversalNode> allNodes = new List<TraversalNode>();
            _visitedElementIds.Clear();

            TraversalNode rootNode = ProcessElement(startElement);
            if (rootNode != null)
            {
                allNodes.Add(rootNode);
                TraverseRecursive(rootNode, allNodes);
            }

            return allNodes;
        }

        private void TraverseRecursive(TraversalNode currentNode, List<TraversalNode> allNodes)
        {
            ConnectorManager cm = GetConnectorManager(currentNode.Element);
            if (cm == null) return;

            foreach (Connector connector in cm.Connectors)
            {
                if (connector.IsConnected)
                {
                    foreach (Connector refConnector in connector.AllRefs)
                    {
                        // РџСЂРѕРїСѓСЃРєР°РµРј РєРѕРЅРЅРµРєС‚РѕСЂС‹, РїСЂРёРЅР°РґР»РµР¶Р°С‰РёРµ С‚РѕРјСѓ Р¶Рµ СЌР»РµРјРµРЅС‚Сѓ
                        if (refConnector.Owner.Id.IntegerValue == currentNode.Element.Id.IntegerValue)
                            continue;

                        // РџСЂРѕРїСѓСЃРєР°РµРј Р»РѕРіРёС‡РµСЃРєРёРµ РєРѕРЅРЅРµРєС‚РѕСЂС‹ (РЅР°РїСЂРёРјРµСЂ, СЃРёСЃС‚РµРјС‹)
                        if (refConnector.ConnectorType == ConnectorType.Logical)
                  
<truncated 2329 bytes>