using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace Koala
{
    public class KoalaComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public KoalaComponent()
          : base("Koala", "hello(*^_^*)/",
              "Create and optimize column forest!",
              "Koala", "optimize")
        {
        }

        //variables that will be shared throughout the script
        static double buckling_length = 10000;
        static double min_angle = 25;
        static double min_dist = 1000;
        static double best_fitness = -1000000;
        static int max_translation = 100;
        static int max_rotation = 100;
        static List<Plane> current_planes = new List<Plane>();
        Box boundary = new Box();
        static List<Line> best_lines;
        static List<double> output_angles;
        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBoxParameter("BoundingBox", "B", "BoundingBox", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Planes", "P", "List of planes", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Indices", "I", "Tree of indices", GH_ParamAccess.tree);
            pManager.AddNumberParameter("BucklingLength", "B", "Buckling length", GH_ParamAccess.item);
            pManager.AddNumberParameter("MinimumAngle", "A", "Minimum Angle", GH_ParamAccess.item);
            pManager.AddNumberParameter("MinimumDistance", "D", "Minimum Distance", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Continue", "C", "Continue simulation", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Reset", "R", "Reset simulation", GH_ParamAccess.item, false);
            pManager.AddIntegerParameter("MaxTranslation", "MT", "Max Translation for a Plane", GH_ParamAccess.item);
            pManager.AddIntegerParameter("MaxRotation", "MR", "Max Rotation for a Planes", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Output lines", GH_ParamAccess.list);
            pManager.AddLineParameter("Segments", "S", "Segments", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "P", "Points", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Fitness", "F", "Fitness of lines", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Planes", "P", "Current Planes", GH_ParamAccess.list);
            pManager.AddNumberParameter("BestFitness", "BF", "Best Fitness", GH_ParamAccess.item);
            pManager.AddLineParameter("BestLines", "BL", "Best lines", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            //declaring variables
            
            List<Plane> list_of_planes = new List<Plane>();
            List<Plane> previous_planes = new List<Plane>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Integer> indices;
            Boolean continue_simulation = true;
            Boolean reset_simulation = false;

            List<Line> output_lines;
            double output_fitness;
            List<Line> output_segments;
            List<List<Point3d>> list_points;
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> output_points;

            //get input data if there are, otherwise abort
            if (!DA.GetData(0, ref boundary)) return;
            if (!DA.GetDataList(1, list_of_planes)) return;
            if (!DA.GetDataTree(2, out indices)) return;
            if (!DA.GetData(3, ref buckling_length)) return;
            if (!DA.GetData(4, ref min_angle)) return;
            if (!DA.GetData(5, ref min_dist)) return;
            if (!DA.GetData(6, ref continue_simulation)) return;
            if (!DA.GetData(7, ref reset_simulation)) return;
            if (!DA.GetData(8, ref max_translation)) return;
            if (!DA.GetData(9, ref max_rotation)) return;
            //if reset button is pressed, it restores original sets of planes
            if (reset_simulation)
            {
                current_planes = new List<Plane>();
                best_fitness = -100000;
            }
            if (current_planes.Count == 0 || list_of_planes != previous_planes)
            {
                current_planes = new List<Plane>(list_of_planes);
            }
            previous_planes = list_of_planes;

            FixPlanes();
            //maybe add warning later regarding types of input data
            //if (a <= b)
            //{
            //    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "some warnings");
            //    return;
            //}

            //optimize!!
            if (continue_simulation)
            {
                TransformPlanes(indices, out output_lines, out output_fitness, out output_segments, out list_points);
                output_points = FromListToTree(list_points);
            }
            else
            {
                //calculate intersection lines from planes
                List<Line> pre_output_lines = GetLines(current_planes, indices);
                //calculate final fitness of the lines
                pre_output_lines = ModifyLines(pre_output_lines, boundary);
                CalculateFitness(pre_output_lines, out output_lines, out output_fitness, out output_segments, out list_points);
                output_points = FromListToTree(list_points);
            }


            //set output data
            DA.SetDataList(0, output_lines);
            DA.SetDataList(1, output_segments);
            DA.SetDataTree(2, output_points);
            DA.SetData(3, output_fitness);
            DA.SetDataList(4, current_planes);
            DA.SetData(5, best_fitness);
            DA.SetDataList(6, best_lines);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return Properties.Resources.koala;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("1019b250-e61d-4f39-b5af-95398384ef46"); }
        }

        private List<Line> GetLines(List<Plane> planes, Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Integer> indices)
        {
            List<Line> lines = new List<Line>();
            List<List<Plane>> new_planes = new List<List<Plane>>();
            foreach (Grasshopper.Kernel.Data.GH_Path path in indices.Paths)
            {
                List<Plane> branch_planes = new List<Plane>();
                foreach(Grasshopper.Kernel.Types.GH_Integer wrapper in indices[path])
                {
                    int index = wrapper.Value;
                    branch_planes.Add(planes[index]);
                }
                new_planes.Add(branch_planes);
                 
                
            }
            for (int i = 0; i < new_planes.Count; i++)
            {
                for(int j = 1; j < new_planes[i].Count; j++)
                {
                    //get an intersection line between planes[i][0] and planes[i][j]
                    bool line_exists = Rhino.Geometry.Intersect.Intersection.PlanePlane(new_planes[i][0], new_planes[i][j], out Line line);
                    if(line_exists)
                    {
                        lines.Add(line);
                       
                    }
                }
            }
            
            return lines;
        }

        private Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> FromListToTree(List<List<Point3d>> points)
        {
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> new_points = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();

            for (int i = 0; i < points.Count; i++)
            {
                for(int j = 0; j < points[i].Count; j++)
                {
                    Grasshopper.Kernel.Types.GH_Point pt = new Grasshopper.Kernel.Types.GH_Point();
                    pt.Value = points[i][j];
                    new_points.Append(pt, new Grasshopper.Kernel.Data.GH_Path(i));
                }
                
            }
            return new_points;
        }

        private double CalculatePlaneAngle(Plane plane)
        {
            double[] plane1 = plane.GetPlaneEquation();
            //double[] plane2 = Plane.WorldXY.GetPlaneEquation();
            Vector3d p1 = new Vector3d(plane1[0], plane1[1], plane1[2]);
            //Vector3d p2 = new Vector3d(plane2[0], plane2[1], plane2[2]);
            p1.Unitize();
            //p2.Unitize();
            //double cos_theta = Math.Abs((p1.X * p2.X + p1.Y * p2.Y * p1.Z + p2.Z) / (p1.Length * p2.Length));
            double cos_theta = p1.Z;
            double theta = Math.Acos(cos_theta % 1);
            double angle = theta * (180.0 / Math.PI);
            angle = Math.Min(angle, 180 - angle);
            return angle;
        }

        private void FixPlanes()
        {
            output_angles = new List<double>();
            foreach (Plane p in current_planes)
            {
                double angle = CalculatePlaneAngle(p);
                //if(angle < min_angle)
                //{
                //    double rotation_angle = min_angle - angle;
                //    rotation_angle = (Math.PI / 180.0) * angle;
                //    Transform transformation_matrix = new Transform();
                //    double cos_a = Math.Cos(rotation_angle);
                //    double sin_a = Math.Sin(rotation_angle);
                //    transformation_matrix.M11 = cos_a;
                //    transformation_matrix.M12 = sin_a;
                //    transformation_matrix.M21 = -sin_a;
                //    transformation_matrix.M22 = cos_a;
                //    p.Transform(transformation_matrix);
                //}
                //output_angles.Add(CalculatePlaneAngle(p));
                output_angles.Add(angle);
            }
        }

        private void TransformPlanes(Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Integer> indices, out List<Line> output_lines, out double output_fitness, out List<Line> output_segments, out List<List<Point3d>> list_points)
        {
            ////record current performance and planes
            List<Line> lines = GetLines(current_planes, indices);
            lines = ModifyLines(lines, boundary);
            CalculateFitness(lines, out List<Line> old_lines, out double old_fitness, out List<Line> segs, out List<List<Point3d>> ps);
            //List<Line> lines = new List<Line>();
            //randomly select one plane and transform
            Random rnd = new Random();
            int index = rnd.Next(current_planes.Count);
            Plane plane = current_planes[index].Clone();
            Plane old_plane = current_planes[index].Clone();


            //calculate performance after the transformation
            //Transform transformation_matrix = Transform.Identity;
            //transformation_matrix.M03 = Convert.ToDouble(rnd.Next(-max_translation, max_translation));
            //transformation_matrix.M13 = Convert.ToDouble(rnd.Next(-max_translation, max_translation));
            //transformation_matrix.M23 = Convert.ToDouble(rnd.Next(-max_translation, max_translation));
            //transformation_matrix.M00 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M01 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M02 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M10 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M11 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M12 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M30 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M31 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //transformation_matrix.M32 = Convert.ToDouble(rnd.Next(-max_rotation, max_rotation) * 0.001);
            //plane.Transform(transformation_matrix);
            Vector3d translation = new Vector3d(Convert.ToDouble(rnd.Next(-max_translation, max_translation)), Convert.ToDouble(rnd.Next(-max_translation, max_translation)), Convert.ToDouble(rnd.Next(-max_translation, max_translation)));
            plane.Translate(translation);
            plane.Rotate(Convert.ToDouble(rnd.Next(-max_rotation, max_rotation)), Vector3d.XAxis);
            plane.Rotate(Convert.ToDouble(rnd.Next(-max_rotation, max_rotation)), Vector3d.YAxis);
            plane.Rotate(Convert.ToDouble(rnd.Next(-max_rotation, max_rotation)), Vector3d.ZAxis);
            current_planes[index] = plane;
            lines = GetLines(current_planes, indices);
            lines = ModifyLines(lines, boundary);
            CalculateFitness(lines, out List<Line> long_lines, out double new_fitness, out List<Line> segs2, out List<List<Point3d>> ps2);
            
            //compare performance between new and old
            if(new_fitness < best_fitness)
            {
                //if old one is better, restore the transformed plane
                current_planes[index] = old_plane;
                output_lines = old_lines;
                output_fitness = old_fitness;
                output_segments = segs;
                list_points = ps;
                
            }
            else
            {
                best_fitness = new_fitness;
                output_lines = long_lines;
                output_fitness = new_fitness;
                output_segments = segs2;
                list_points = ps2;
                best_lines = new List<Line>(long_lines);
            }

        }

        private List<Line> ModifyLines(List<Line> lines, Box boundary)
        {
            //it modifies the original lines so that each line starts at one side of the volume and ends at other side of the volume
            List<Line> new_lines = new List<Line>();
            foreach(Line line in lines)
            {
                Rhino.Geometry.Intersect.Intersection.LineBox(line, boundary, 0.0, out Interval lineParameters);
                Line new_line = new Line(line.PointAt(lineParameters[0]), line.PointAt(lineParameters[1]));
                if(new_line.FromZ > new_line.ToZ)
                {
                    //the start point of the line is always lower than the end point
                    new_line.Flip();
                }
                new_lines.Add(new_line);
            }

            return new_lines;
        }

        private void CalculateFitness(List<Line> input_lines, out List<Line> lines, out double fitness, out List<Line> segments, out List<List<Point3d>> nested_points)
        {
            fitness = 0;
            lines = new List<Line>();
            segments = new List<Line>();
            nested_points = new List<List<Point3d>>();
            //calculate number of segments that are longer than buckling length, too close to the others, and too flat
            int num_too_long = 0;
            int num_too_close = 0;
            int num_too_flat = 0;
            
            
            foreach(Line line in input_lines)
            {
                List<Point3d> points = new List<Point3d>();
                points.Add(line.From);
                foreach (Line other in input_lines)
                {
                    if(line != other)
                    {
                        //check line-line intersection
                        double dist = line.MinimumDistanceTo(other);
                        if(dist < 1.0)
                        {
                            Rhino.Geometry.Intersect.Intersection.LineLine(line, other, out double a, out double b);
                            points.Add(line.PointAt(a));
                        }
                        //check distances at both ends
                        List<double> distances = new List<double>();
                        distances.Add(line.From.DistanceTo(other.From));
                        distances.Add(line.From.DistanceTo(other.To));
                        distances.Add(line.To.DistanceTo(other.From));
                        distances.Add(line.To.DistanceTo(other.To));
                        foreach(double d in distances)
                        {
                            if(d < min_dist)
                            {
                                num_too_close += 1;
                            }
                        }
                    }
                    
                }
                points.Add(line.To);
                points= points.OrderBy(p => p.Z).ToList();
                if(points.Count < 3)
                {
                    //if the line does not have any connection, ignore it
                    continue;
                }
                lines.Add(line);
                nested_points.Add(points);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    double dist = points[i].DistanceTo(points[i + 1]);
                    Line segment = new Line(points[i], points[i + 1]);
                    segments.Add(segment);
                    if(dist > buckling_length)
                    {
                        num_too_long += 1;
                    }
                }
                //check angle
                Vector3d start = new Vector3d(line.FromX, line.FromY, line.FromZ);
                Vector3d end = new Vector3d(line.ToX, line.ToY, line.ToZ);
                Vector3d projected = new Vector3d(line.ToX, line.ToY, line.FromZ);
                Vector3d vector_line = Vector3d.Subtract(end, start);
                Vector3d vector_projected = Vector3d.Subtract(projected, start);
                double angle = Vector3d.VectorAngle(vector_line, vector_projected);
                if(angle < min_angle)
                {
                    num_too_flat += 1;
                }
            }
            //calculate fitness with factors
            int sum = num_too_long + num_too_close + num_too_flat;
            fitness = input_lines.Count - sum;
            
            
      
        }
    }
}
