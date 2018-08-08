using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace camera3D.Util
{
    class PLYUtil
    {
        private string savedir;
        public PLYUtil(string savedir)
        {
            this.savedir = savedir;
        }
        public int PLYWriter(List<double[,]> pointList,string fileName)
        {
            FileStream fs = null;
            try
            {
                fs = new FileStream(savedir+"/"+fileName, FileMode.Create);
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine("ply");
                    sw.WriteLine("format ascii 1.0");
                    sw.WriteLine("element vertex {0}",pointList.Count);
                    sw.WriteLine("property float x");
                    sw.WriteLine("property float y");
                    sw.WriteLine("property float z");
                    sw.WriteLine("end_header");
                    foreach(double[,] point in pointList)
                    {
                        sw.WriteLine("{0} {1} {2}",point[0,0],point[0,1],point[0,2]);
                    }
                }
            }
            catch(Exception)
            {
                return -1;
            }
            finally
            {
                if (fs != null)
                    fs.Dispose();
            }
            return 0;
        }
    }
}
