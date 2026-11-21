using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace Autodesk.Revit.DB
{
    public class ElementId { public int Value; public ElementId(int value) { Value=value; } }
    public enum StorageType { Double, String }
    public enum ViewDetailLevel { Fine }
    public static class SpecTypeId { public static object Length = new object(); }
    public class Definition { public object Spec = SpecTypeId.Length; public object GetDataType() => Spec; }
    public class Parameter
    {
        public StorageType StorageType = StorageType.Double;
        public bool IsReadOnly, Reject;
        public bool HasValue = true;
        public Definition Definition = new Definition();
        public double Value;
        public bool IgnoreAcceptedWrites;
        private double? _pendingValue;
        public double AsDouble() => Value;
        public bool Set(double value)
        {
            if (Reject) return false;
            if (!IgnoreAcceptedWrites) _pendingValue = value;
            return true;
        }

        public void Regenerate()
        {
            if (!_pendingValue.HasValue) return;
            Value = _pendingValue.Value;
            _pendingValue = null;
        }
    }
    public class XYZ { public double X,Y,Z; public XYZ(double x,double y,double z) {X=x;Y=y;Z=z;} }
    public class Options { public ViewDetailLevel DetailLevel; }
    public class GeometryObject { }
    public class GeometryElement : GeometryObject, IEnumerable<GeometryObject>
    {
        public List<GeometryObject> Items = new List<GeometryObject>();
        public IEnumerator<GeometryObject> GetEnumerator()=>Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public class Mesh : GeometryObject { public IList<XYZ> Vertices; }
    public class Face { public Mesh Mesh; public Mesh Triangulate()=>Mesh; }
    public class Solid : GeometryObject { public double Volume=1; public List<Face> Faces=new List<Face>(); }
    public class GeometryInstance : GeometryObject { public GeometryElement Geometry; public GeometryElement GetInstanceGeometry()=>Geometry; }
    public class Element
    {
        public ElementId Id;
        public virtual GeometryElement get_Geometry(Options options)=>null;
    }
    public class Level : Element { public double ProjectElevation; }
    public class FamilyInstance : Element
    {
        public ElementId LevelId;
        public Dictionary<string,Parameter> Parameters = new Dictionary<string,Parameter>();
        public double Z, AnchorFraction=0.5;
        public bool NoGeometry, Nested, RejectMove, HeightDoesNotDriveGeometry;
        public IList<Parameter> GetParameters(string name)=>Parameters.TryGetValue(name,out var p)?new[]{p}:new Parameter[0];
        public double Height => HeightDoesNotDriveGeometry ? 1 : Parameters["H"].Value;
        public double Bottom => Z-AnchorFraction*Height;
        public double Top => Bottom+Height;
        public override GeometryElement get_Geometry(Options options)
        {
            var geometry=new GeometryElement();
            if(NoGeometry) return geometry;
            var solid=new Solid();
            solid.Faces.Add(new Face { Mesh=new Mesh { Vertices=new[]{new XYZ(0,0,Bottom),new XYZ(1,1,Top)} } });
            geometry.Items.Add(solid);
            if(!Nested) return geometry;
            var wrapper=new GeometryElement(); wrapper.Items.Add(new GeometryInstance {Geometry=geometry}); return wrapper;
        }
    }
    public class Document
    {
        public List<Element> Elements=new List<Element>();
        public Element GetElement(ElementId id)=>id==null?null:Elements.FirstOrDefault(e=>e.Id.Value==id.Value);
        public void Regenerate()
        {
            foreach (var parameter in Elements.OfType<FamilyInstance>().SelectMany(instance => instance.Parameters.Values))
                parameter.Regenerate();
        }
    }
    public class FilteredElementCollector : IEnumerable<Element>
    {
        private IEnumerable<Element> _elements;
        public FilteredElementCollector(Document doc) { _elements=doc.Elements; }
        public FilteredElementCollector OfClass(Type type) { _elements=_elements.Where(e=>type.IsInstanceOfType(e)); return this; }
        public IEnumerator<Element> GetEnumerator()=>_elements.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public static class ElementTransformUtils
    {
        public static void MoveElement(Document doc,ElementId id,XYZ delta)
        {
            var instance=(FamilyInstance)doc.GetElement(id);
            if(!instance.RejectMove) instance.Z+=delta.Z;
        }
    }
}
