﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Tigera.LibCommon;
using TigEra.DocScaner.Definition;

namespace TigEra.DocScaner.Adapter.PBTwain
{
    public partial class FormImageScaner : Form, IMessageFilter
    {
        private twain twainDevice;
        private bool msgfilter;

        public void HideMode
            ()
        {
            this.Size = new System.Drawing.Size(0, 0);

            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.ShowInTaskbar = false;
            this.ShowIcon = false;
            this.MinimizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.Size = new System.Drawing.Size(0, 0);
            this.Opacity = 0.0;
            this.Visible = false;

            this.AutoScaleDimensions = (System.Drawing.SizeF)new System.Drawing.SizeF(5, 13);
            this.WindowState = FormWindowState.Minimized;

            this.ShowInTaskbar = false;
            this.ClientSize = new Size(0, 0);
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "HiddenForm";
        }

        public FormImageScaner(IAcquirerParam param)
        {
            InitializeComponent();
            twainDevice = new twain();
           
            this.ShowInTaskbar = false;
            this.DialogResult = DialogResult.Cancel;
            this.Text = "扫描";
            this.Load += FormImageScaner_Load;
            this.MinimumSizeChanged += FormImageScaner_MinimumSizeChanged;
        }

        private void FormImageScaner_MinimumSizeChanged(object sender, EventArgs e)
        {
            HideMode();
            this.Visible = false;
        }

        private void FormImageScaner_Load(object sender, EventArgs e)
        {
            btnScan_Click(this, EventArgs.Empty);
        }

        private void EndingScan(bool Canceled)
        {
            if (msgfilter)
            {
                Application.RemoveMessageFilter(this);
                msgfilter = false;
                this.Enabled = true;
                this.Activate();
                if (Canceled) this.Show();
            }
        }

        public PBTwainAcquirer.NestSetting Setting
        {
            get;
            set;
        }

        private void SaveToFile(int i, Bitmap bmp)
        {
            if (this.Setting == null)
                return;
            String fname = this.Setting.ImageDir + DateTime.Now.ToYMDHMS() + i.ToString() + "." + this.Setting.FType.ToString();

            EncoderParameter p;
            EncoderParameters ps;
            ps = new EncoderParameters(1);
            p = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, this.Setting.ImgRatio);
            ps.Param[0] = p;

            bmp.Save(fname, this.Setting.FType.ToImageCodecInfo(), ps);

            if (File.Exists(fname))
            {
                TmpFileMgr.AddTmpFile(fname);
            }

            _images.Add(fname);
        }

        bool IMessageFilter.PreFilterMessage(ref Message m)
        {
            TwainCommand cmd = twainDevice.PassMessage(ref m);
            if (cmd == TwainCommand.Not)
                return false;

            switch (cmd)
            {
                case TwainCommand.CloseRequest:
                    {
                        EndingScan(true);
                        twainDevice.CloseSrc();
                        this.btnOk_Click(this, EventArgs.Empty);
                        break;
                    }
                case TwainCommand.CloseOk:
                    {
                        EndingScan(false);
                        twainDevice.CloseSrc();
                        this.btnOk_Click(this, EventArgs.Empty);
                        break;
                    }
                case TwainCommand.DeviceEvent:
                    {
                        break;
                    }
                case TwainCommand.TransferReady:
                    {
                        ArrayList pics = twainDevice.TransferPictures();

                        EndingScan(false);
                        twainDevice.CloseSrc();

                        if (pics.Count > 0)
                        {
                            int i, j = 1;

                            String DocumentID = String.Empty;
                            this.Cursor = Cursors.WaitCursor;

                            for (i = 0; i < pics.Count; i++)
                            {
                                IntPtr img = (IntPtr)pics[i];
                                Bitmap bmp = GetBitmap(img);

                                if (img != IntPtr.Zero)
                                {
                                    imageBoxCtrl.pictureStrip1.AddPicture(bmp);
                                    SaveToFile(i, bmp);

                                    imageBoxCtrl.imageBox.Image = bmp;
                                    imageBoxCtrl.imageBox.Size = new Size(bmp.Width, bmp.Height);
                                }
                            }

                            j++;
                        }
                        this.Cursor = Cursors.Arrow;
                        this.btnOk_Click(this, EventArgs.Empty);
                        break;
                    }
            }

            return true;
        }

        private List<String> _images = new List<string>();

        public List<String> Images
        {
            get
            {
                return _images;
            }
        }

        public Bitmap GetBitmap(IntPtr adibPtr)
        {
            IntPtr dibPtr;
            BITMAPFILEHEADER fh = new BITMAPFILEHEADER();
            dibPtr = GlobalLock(adibPtr);
            Type bmiTyp = typeof(BITMAPINFOHEADER);
            BITMAPINFOHEADER bmi = (BITMAPINFOHEADER)Marshal.PtrToStructure(dibPtr, bmiTyp);

            if (bmi.biSizeImage == 0)
                bmi.biSizeImage = ((((bmi.biWidth * bmi.biBitCount) + 31) & ~31) >> 3) * Math.Abs(bmi.biHeight);
            if ((bmi.biClrUsed == 0) && (bmi.biBitCount < 16))
                bmi.biClrUsed = 1 << bmi.biBitCount;

            int fhSize = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
            int dibSize = bmi.biSize + (bmi.biClrUsed * 4) + bmi.biSizeImage;

            fh.Type = new Char[] { 'B', 'M' };
            fh.Size = fhSize + dibSize;
            fh.OffBits = fhSize + bmi.biSize + (bmi.biClrUsed * 4);

            byte[] data = new byte[fh.Size];
            RawSerializeInto(fh, data);
            Marshal.Copy(dibPtr, data, fhSize, dibSize);
            MemoryStream stream = new MemoryStream(data);
            Bitmap tmp = new Bitmap(stream);
            Bitmap result = new Bitmap(tmp);
            tmp.Dispose();
            tmp = null;
            stream.Close();
            stream = null;
            data = null;
            GlobalFree(adibPtr);
            return result;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Application.AddMessageFilter(this);
            //this.Hide();
            if (msgfilter == false)
            {
                msgfilter = true;
                // -1 means to multipage scan
                if (false == twainDevice.Acquire(-1))
                {
                    msgfilter = false;
                    Application.RemoveMessageFilter(this);
                    this.Enabled = true;
                    this.Activate();

                    this.Show();
                }
            }
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            twainDevice.Select();
        }

        private void imageScan_ResizeBegin(object sender, EventArgs e)
        {
            //int newHeight, newWidth;

            //if (imageBox.image.Height > imageBox.image.Width)
            //{
            //newWidth = (int)(((double)imageBoxCtrl.imageBox.Image.Width / (double)imageBoxCtrl.imageBox.Image.Height) * (double)this.Height);
            //this.imageBoxCtrl.imageBox.Width = (int)(((double)imageBoxCtrl.imageBox.Image.Width / (double)imageBoxCtrl.imageBox.Image.Height) * (double)(this.Height - imageBoxCtrl.imageBox.Location.Y));
            //}
            //else
            //{
            //newHeight = (int)(((double)imageBoxCtrl.imageBox.Image.Height / (double)imageBoxCtrl.imageBox.Image.Width) * (double)this.Width);
            //this.imageBoxCtrl.imageBox.Height = (int)(((double)imageBoxCtrl.imageBox.Image.Height / (double)imageBoxCtrl.imageBox.Image.Width) * (double)this.Width);
            //}
        }

        private void RawSerializeInto(object anything, byte[] datas)
        {
            int rawsize = Marshal.SizeOf(anything);

            if (rawsize > datas.Length)
                throw new ArgumentException(" buffer too small ", " byte[] datas ");

            GCHandle handle = GCHandle.Alloc(datas, GCHandleType.Pinned);
            IntPtr buffer = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(anything, buffer, false);
            handle.Free();
        }

        #region win32_struct

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private class BITMAPFILEHEADER
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public Char[] Type;

            public Int32 Size;
            public Int16 reserved1;
            public Int16 reserved2;
            public Int32 OffBits;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private class BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            twainDevice.Init(this.Handle);
            toolStripButton1_Click(this, EventArgs.Empty);
        }

        #endregion win32_struct

        #region win32_api_defs

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdiplus.dll", ExactSpelling = true)]
        private static extern int GdipCreateBitmapFromGdiDib(IntPtr bminfo, IntPtr pixdat, ref IntPtr image);

        [DllImport("gdiplus.dll", ExactSpelling = true)]
        private static extern int GdipCreateHBITMAPFromBitmap(IntPtr image, out IntPtr hbitmap, int bkg);

        [DllImport("gdiplus.dll", ExactSpelling = true)]
        private static extern int GdipDisposeImage(IntPtr image);

        [DllImport("gdiplus.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int GdipSaveImageToFile(IntPtr image, string filename, [In] ref Guid clsid, IntPtr encparams);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GlobalLock(IntPtr handle);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GlobalFree(IntPtr handle);

        #endregion win32_api_defs
    }
}