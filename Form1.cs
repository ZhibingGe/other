using AForge.Imaging.Filters;
using AForge.Video.DirectShow;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CameraTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices != null && videoDevices.Count > 0)
            {
                int idx = 0;
                foreach (FilterInfo device in videoDevices)
                {
                    comboBox1.Items.Add(device.Name);
                    idx++;
                }
                comboBox1.SelectedIndex = comboBox1.Items.Count - 1;
            }
            var mon = videoDevices[videoDevices.Count - 1].MonikerString;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "连接")
            {
                FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                var mon = videoDevices[videoDevices.Count - 1].MonikerString;
                Size frameSize = new Size(1280, 720);
                VideoCaptureDevice captureAForge = new VideoCaptureDevice(videoDevices[videoDevices.Count - 1].MonikerString);
                captureAForge.VideoResolution = captureAForge.VideoCapabilities[0];
                videoSourcePlayer1.VideoSource = captureAForge;
                videoSourcePlayer1.Start();
                button1.Text = "关闭";
            }
            else
            {
                videoSourcePlayer1.SignalToStop();
                videoSourcePlayer1.WaitForStop();
                videoSourcePlayer1.Stop();
                button1.Text = "连接";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            DirectoryInfo dr = new DirectoryInfo("img/");
            FileInfo[] fr = dr.GetFiles();
            foreach (var item in fr)
            {
                if(item.Name.IndexOf("-2")==-1)
                listBox1.Items.Add(item.Name);
            }

            

        }


        private void DrawBorder(int v1, int v2, int v3, int v4)
        {
            int r1 = v2 - v1;
            int c1 = v4 - v3;
            groupBox1.Location = new Point(videoSourcePlayer1.Location.X + v1 * 3 / 8, videoSourcePlayer1.Location.Y + v3 * 3 / 8);
            groupBox2.Location = new Point(videoSourcePlayer1.Location.X + v1 * 3 / 8, videoSourcePlayer1.Location.Y + v3 * 3 / 8);
            groupBox3.Location = new Point(videoSourcePlayer1.Location.X + v1 * 3 / 8+r1*3/8, videoSourcePlayer1.Location.Y + v3 * 3 / 8);
            groupBox4.Location = new Point(videoSourcePlayer1.Location.X + v1 * 3 / 8, videoSourcePlayer1.Location.Y + v3 * 3 / 8+c1*3/8);
            groupBox1.Size = new Size(2, c1*3/8);
            groupBox2.Size = new Size(r1 * 3 / 8, 2);
            groupBox3.Size = new Size(2, c1 * 3 / 8);
            groupBox4.Size = groupBox2.Size;


        }


        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //获取一帧图像
            Bitmap bit =(Bitmap) Image.FromFile(Application.StartupPath+"/img/"+listBox1.Text);
            videoSourcePlayer1.BackgroundImage = bit;
            var a=CalResult(bit,listBox1.Text);
            //CalResult1(bit);
            if (MessageBox.Show(a, "", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                File.Move(Application.StartupPath + "/img1/" + listBox1.Text, Application.StartupPath + "/img1/done/" + listBox1.Text);
            }
        }

        private string CalResult(Bitmap bit,string path)
        {
            //用于记录原始的解析信息
            var str = "";
            try
            {
                //灰度化处理，人眼对绿色敏感度最高，蓝色敏感度最低，所以权重侧重绿色
                bit = new Grayscale(0.2125, 0.7154, 0.0721).Apply(bit);
                //BitmapData 可以提高效率，直接读取灰度值的方法耗时800ms，bitmapdata只需要10ms。
                var mapdata = BitToByte(bit);
                //列相加
                byte[] col = SumCol(mapdata, bit.Width, bit.Height);
                //用于寻找上下方向边框
                int[] colx = new int[2] { ColBorder(col) - 50, ColBorder(col) + 50 };
                //行相加，用于寻找左右方向边框
                byte[] row = SumRow(mapdata, bit.Width, colx[0],colx[1]);
                //得到边框位置，如果为白色试剂片，需重新设计算法
                int[] rowx = RowBorder(row);
                //考虑边缘阴影等，切除部分阴影
                rowx[0] += 10;
                rowx[1] -= 10;
                //截取试剂条区域
                Rectangle rect = new Rectangle(new Point(rowx[0], colx[0]), new Size((rowx[1] - rowx[0]) / 4 * 4, colx[1] - colx[0]));
                Bitmap b = new Bitmap((rowx[1] - rowx[0]) / 4 * 4, colx[1] - colx[0], PixelFormat.Format32bppArgb);
                Graphics g = Graphics.FromImage(b);
                g.DrawImage(bit, 0, 0, rect, GraphicsUnit.Pixel);
                g.Dispose();
                //灰度化处理
                b = new Grayscale(0.2125, 0.7154, 0.0721).Apply(b);
                //二值化处理
                b = new Threshold(180).Apply(b);
                pictureBox2.Image = b;
                //取得新的截取后区域的值
                mapdata = BitToByte(b);
                //进行行相加
                row = SumRow(mapdata, b.Width, 0,b.Height);
                col = SumCol(mapdata, b.Width, b.Height);
                for (int i = 0; i < row.Length; i++)
                {
                    row[i] = (byte)(row[i] > 128 ? 255 : 0);
                }
                //寻找左右两条参考黑线
                int[] FirstEndX = LookFirstEndBlackLine(row);
                double startx = FirstEndX[0];
                double endx = FirstEndX[1];
                //24条线的线宽计算
                double index = (int)((endx - startx + 1) / 24.00 + 0.5);
                //黑白临界点位置
                List<int> result = new List<int>();
                //黑白线宽  交替
                List<double> result1 = new List<double>();
                int flag = 0;
                result.Add((int)startx);
                str = startx + ",";
                for (int i = (int)startx; i < endx + 1; i++)
                {
                    if (flag == 0 & row[i] == 255)
                    {
                        flag = 255;
                        result.Add(i);
                        result1.Add(i - result[result.Count - 2]);
                        str += i.ToString() + ",";
                    }
                    else if (flag == 255 & row[i] == 0)
                    {
                        flag = 0;
                        result.Add(i);
                        result1.Add(i - result[result.Count - 2]);
                        str += i.ToString() + ",";
                    }
                }
                result.Add((int)endx);
                result1.Add(endx - result[result.Count - 2]);
                str += endx.ToString();
                //如果有很窄的白线或者黑线不到1格，需要处理
                while ((int)(result1.Min() / index + 0.5) == 0)
                {
                    for (int i = 0; i < result1.Count; i++)
                    {
                        if (result1[i] == result1.Min())
                        {
                            if (i == 0)
                            {
                                result1[i] += result1[i + 1];
                                result1.RemoveAt(i + 1);
                                break;
                            }
                            else if (i == result1.Count - 1)
                            {
                                result1[i] += result1[i - 1];
                                result1.RemoveAt(i - 1);
                                break;
                            }
                            else
                            {
                                if (result1[i + 1] < index)
                                {
                                    result1[i] += result1[i + 1];
                                    result1.RemoveAt(i + 1);
                                    break;
                                }
                                else if (result1[i - 1] < index)
                                {
                                    result1[i] += result1[i - 1];
                                    result1.RemoveAt(i - 1);
                                    break;
                                }
                                else
                                {
                                    if ((int)result1[i - 1] % (int)index < (int)result1[i + 1] % (int)index)
                                    {
                                        result1[i - 1] += result1[i];
                                        result1.RemoveAt(i);
                                        break;
                                    }
                                    else
                                    {
                                        result1[i + 1] += result1[i];
                                        result1.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                for (int i = 0; i < result1.Count; i++)
                {
                    result1[i] = (int)(((double)result1[i] / ((double)index) + 0.5));
                    str += result1[i].ToString() + "\r\n";
                }
                var result2 = "";
                char flag1 = '1';
                for (int i = result1.Count - 1; i > -1; i--)
                {
                    result2 += "".PadLeft((int)result1[i], flag1);
                    flag1 = flag1 == '1' ? '0' : '1';
                }
                if (result2.Length == 24)
                {
                    if (result2.Substring(0, 2) == "10" & result2.Substring(22, 2) == "01")
                    {
                        result2 = result2.Substring(2, 20);

                        int no = Convert.ToInt32(result2, 2);
                        result2 = result2.Reverse().ToString(); ;
                        result2 = no.ToString("X6");
                    }
                }
                return result2+","+(rowx[1]-rowx[0]).ToString()+","+(colx[1]-colx[0]).ToString();
            }
            catch (Exception ee)
            {
                return ee.Message;
            }
        }

        private int[] LookFirstEndBlackLine(byte[] row)
        {
            int[] x = new int[2] { 0, row.Length - 1 };
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == 0)
                {
                    x[0] = i; break;
                }
            }
            for (int i = row.Length - 1; i > -1; i--)
            {
                if (row[i] == 0)
                {
                    x[1] = i; break;
                }
            }
            return x;
        }

        private int ColBorder(byte[] col)
        {
            int[] colx =new int[2];
            for (int i = 15; i < col.Length - 300; i++)
            {
                if (col.Take(300 + i).Reverse().Take(300).Max() - col.Take(300 + i).Reverse().Take(300).Min() < 60)
                {
                    colx[0] = i;break;
                }
            }
            col = col.Reverse().ToArray();
            for (int i = 15; i < col.Length - 300; i++)
            {
                if (col.Take(300 + i).Reverse().Take(300).Max() - col.Take(300 + i).Reverse().Take(300).Min() < 60)
                {
                    colx[1] = col.Length-1-i; break;
                }
            }
            return (int)colx.Average();
        }

        private int[] RowBorder(byte[] row)
        {
            int[] rowx = new int[2];
            for (int i = 15; i < row.Length - 15; i++)
            {
                if (row.Take(i + 15).Reverse().Take(30).Max() == row[i]&row[i]+40>row.Max())
                {
                    rowx[0] = i;break;
                }
            }

            row = row.Reverse().ToArray();
            for (int i = 15; i < row.Length - 15; i++)
            {
                if (row.Take(i + 15).Reverse().Take(30).Max() == row[i] & row[i] + 40 > row.Max())
                {
                    rowx[1] =row.Length-1- i;break;
                }
            }
            return rowx;
        }

        private byte[] SumCol(byte[] rawdata, int width, int height)
        {
            var col = new byte[height];
            for (int i = 0; i < height; i++)
            {
                var data = 0;
                for (int j = 0; j < width; j++)
                {
                    data += rawdata[i * width + j];
                }
                data = data / width;
                col[i] = (byte)data;
            }
            return col;
        }

        private byte[] SumRow(byte[] rawdata, int width, int col1,int col2)
        {
            var row = new byte[width];
            for (int i = 0; i < width; i++)
            {
                var data = 0;
                for (int j = col1; j < col2; j++)
                {
                    data += rawdata[i + width * j];
                }
                data /= (col2-col1);
                row[i] = (byte)data;
            }
            return row;
        }

        private byte[] BitToByte(Bitmap bit)
        {
            BitmapData bmpdata = bit.LockBits(new System.Drawing.Rectangle(0, 0, bit.Width, bit.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            int stride = bmpdata.Stride;
            int offset = stride - bit.Width;
            IntPtr iptr = bmpdata.Scan0;
            int scanBytes = stride * bit.Height;
            byte[] mapdata = new byte[scanBytes];
            bit.UnlockBits(bmpdata);
            System.Runtime.InteropServices.Marshal.Copy(iptr, mapdata, 0, scanBytes);
            return mapdata;
        }
        private void button3_Click(object sender, EventArgs e)
        {
            var ssss = "";
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                //获取一帧图像
                Bitmap bit = (Bitmap)Image.FromFile(Application.StartupPath + "/img/" + listBox1.Items[i].ToString());
                videoSourcePlayer1.BackgroundImage = bit;
                ssss+=listBox1.Items[i].ToString()+","+ CalResult(bit,listBox1.Items[i].ToString())+"\r\n";
                Text = (i * 1000 / listBox1.Items.Count).ToString();
                ///resullllt+= CalResult(bit)+"\r\n";
            }
            StreamWriter sw = new StreamWriter("result.csv");
            sw.Write(ssss);
            sw.Close();
        }

        private Bitmap CutImage(Bitmap bit,Rectangle rect)
        {
            //截取试剂条区域
            Bitmap b = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(b);
            g.DrawImage(bit, 0, 0, rect, GraphicsUnit.Pixel);
            g.Dispose();
            return b;
        }

    }
}
