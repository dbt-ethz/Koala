using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Koala
{
    public class KoalaInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "Koala";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("2de991bd-2e5e-4fd5-acc4-93b8c67343c9");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "";
            }
        }
    }
}
