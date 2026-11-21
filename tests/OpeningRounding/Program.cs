using System;
using Autodesk.Revit.DB;
using BimboClub;
internal static class Program
{
    private static int checks;
    private static void Check(bool ok,string message) {if(!ok)throw new Exception(message);checks++;}
    private static bool Near(double a,double b)=>Math.Abs(a-b)<1e-7;
    private static void Fails(Action action,string message)
    { try {action();} catch(InvalidOperationException) {checks++;return;} throw new Exception(message); }
    private static void Main()
    {
        Check(Near(OpeningRoundingOptions.Up(301,10),310),"Size up");
        Check(Near(OpeningRoundingOptions.Up(300.00000000001,10),300),"Exact multiple after floating conversion");
        Check(Near(OpeningRoundingOptions.Down(-1,10),-10),"Negative bottom floors, not truncates");
        Check(Near(OpeningRoundingOptions.Down(-10.00000000001,10),-10),"Exact negative multiple tolerance");
        Check(Near(OpeningRoundingOptions.Up(25.5,0),25.5),"Zero disables rounding");
        Check(Near(OpeningRoundingOptions.Up(120.00012,10,0.0003),120),"Dimension noise does not add a full step");
        Check(Near(OpeningRoundingOptions.Down(2459.99988,10,0.0003),2460),"Geometry noise does not lower the bottom by a full step");
        Check(Near(OpeningRoundingOptions.Up(120.001,10,0.0003),130),"A measurable excess still rounds up");
        foreach(var text in new[]{"10","2.5","2,5","0"}) Check(OpeningRoundingOptions.TryParseStep(text,out _),"Valid step");
        foreach(var text in new[]{"-1","NaN","Infinity","","abc"}) Check(!OpeningRoundingOptions.TryParseStep(text,out _),"Reject invalid step");
        var doc=new Document(); var level=new Level {Id=new ElementId(1),ProjectElevation=7.123}; doc.Elements.Add(level);
        foreach(double anchor in new[]{0.0,0.5,1.0})
        foreach(double offset in new[]{-0.05,0.0,1.234})
        {
            var hole=new FamilyInstance {Id=new ElementId(100+checks),LevelId=level.Id,Z=level.ProjectElevation+offset,AnchorFraction=anchor,Nested=true};
            hole.Parameters["W"]=new Parameter{Value=0.973}; hole.Parameters["H"]=new Parameter{Value=1.137}; hole.Parameters["D"]=new Parameter{Value=0.412};
            doc.Elements.Add(hole); double bottom=hole.Bottom,top=hole.Top;
            OpeningRoundingService.Apply(doc,hole,"W","H","D",new OpeningRoundingOptions(),level);
            Check(Near((hole.Bottom-level.ProjectElevation)*304.8,OpeningRoundingOptions.Down((bottom-level.ProjectElevation)*304.8,10)),"Actual geometric bottom relative to level");
            Check(hole.Bottom<=bottom+1e-7 && hole.Top>=top-1e-7,"Envelope preserved regardless of family origin");
            foreach(var p in hole.Parameters.Values) Check(Near(p.Value*304.8,OpeningRoundingOptions.Up(p.Value*304.8,10)),"All dimensions on grid");
            double repeatedBottom=hole.Bottom,repeatedHeight=hole.Height;
            OpeningRoundingService.Apply(doc,hole,"W","H","D",new OpeningRoundingOptions(),level);
            Check(Near(repeatedBottom,hole.Bottom)&&Near(repeatedHeight,hole.Height),"Idempotent rounding");
        }
        var unsupported=new FamilyInstance {Id=new ElementId(900),LevelId=level.Id,Z=8};doc.Elements.Add(unsupported);
        unsupported.Parameters["W"]=new Parameter {Value=1};unsupported.Parameters["H"]=new Parameter {Value=1};unsupported.Parameters["D"]=new Parameter {Value=1};
        unsupported.NoGeometry=true;
        Fails(()=>OpeningRoundingService.Apply(doc,unsupported,"W","H","D",new OpeningRoundingOptions(),level),"Missing geometry must not report success");
        OpeningRoundingService.Apply(doc,unsupported,"W","H","D",new OpeningRoundingOptions {BottomStepMm=0},level);
        Check(true,"Size-only rounding does not require geometry");
        unsupported.NoGeometry=false; unsupported.Parameters["H"].IsReadOnly=true;
        Fails(()=>OpeningRoundingService.Apply(doc,unsupported,"W","H","D",new OpeningRoundingOptions(),level),"Readonly parameter must abort");
        unsupported.Parameters["H"].IsReadOnly=false;unsupported.Parameters["H"].Reject=true;unsupported.Parameters["H"].Value=1.01;
        Fails(()=>OpeningRoundingService.Apply(doc,unsupported,"W","H","D",new OpeningRoundingOptions(),level),"Rejected write must abort");
        unsupported.Parameters["H"].Reject=false;
        unsupported.Parameters["H"].IgnoreAcceptedWrites=true;
        Fails(()=>OpeningRoundingService.Apply(doc,unsupported,"W","H","D",new OpeningRoundingOptions {BottomStepMm=0},level),"An accepted write must still be verified after regeneration");
        unsupported.Parameters["H"].IgnoreAcceptedWrites=false;unsupported.RejectMove=true;unsupported.Z=8.123;
        Fails(()=>OpeningRoundingService.Apply(doc,unsupported,"W","H","D",new OpeningRoundingOptions(),level),"Constrained placement must not report rounded bottom");
        Check(OpeningRoundingService.FindLevel(doc,0)==level,"Below all levels uses lowest project level");
        Check(OpeningRoundingService.FindLevel(doc,20,level.Id)==level,"Preserves preferred level");
        Console.WriteLine($"PASS: {checks} rounding checks with production service and API test double; no Revit/models.");
    }
}
