using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;
using Emgu;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.GPU;
using Emgu.CV.Features2D;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace Automator
{
	public class ScreenReader
	{
		[DllImport("user32.dll")]
		static extern bool GetCursorPos(ref System.Drawing.Point lpPoint);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
		static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

		Bitmap ScreenPixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
		public Color GetColorAtPoint(System.Drawing.Point location)
		{
			using (Graphics gdest = Graphics.FromImage(ScreenPixel))
			{
				using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
				{
					IntPtr hSrcDC = gsrc.GetHdc();
					IntPtr hDC = gdest.GetHdc();
					int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, location.X, location.Y, (int)CopyPixelOperation.SourceCopy);
					gdest.ReleaseHdc();
					gsrc.ReleaseHdc();
				}
			}
			
			return ScreenPixel.GetPixel(0, 0);
		}

		public static Color GetColorAt(int x, int y)
		{
			return new ScreenReader().GetColorAtPoint(new System.Drawing.Point(x, y));
		}
		public static Color GetColorAt(System.Drawing.Point location)
		{
			return new ScreenReader().GetColorAtPoint(location);
		}
		public static bool MatchesColors(Color a, Color[] b, int tolerance)
		{
			if (b == null) return false;
			foreach (Color c in b)
			{
				if (ColorWithinTolerance(a, c, tolerance))
					return true;
			}
			return false;
		}

		public static System.Drawing.Point GetSizeOfColorRegion(int x, int y, int tolerance, Color[] ignore = null, int stopgap = 0)
		{
			Bitmap pic = GetScreen();
			Color basecolor = pic.GetPixel(x, y);
			int width = 0, height = 0;

			int gap = 0;
			for (int i = x; i >= 0; i--)
			{
				Color current = pic.GetPixel(i, y);
				if (ColorWithinTolerance(current, basecolor, tolerance) || MatchesColors(current, ignore, tolerance))
				{
					width++;
					gap = 0;
				}
				else
				{
					gap++;
					if (gap >= stopgap)
						break;
				}
					
			}
			gap = 0;
			for (int i = x+1; i < pic.Width; i++)
			{
				Color current = pic.GetPixel(i, y);
				if (ColorWithinTolerance(current, basecolor, tolerance) || MatchesColors(current, ignore, tolerance))
				{
					width++;
					gap = 0;
				}
				else
				{
					gap++;
					if (gap >= stopgap)
						break;
				}
			}
			gap = 0;
			for (int i = y; i >= 0; i--)
			{
				Color current = pic.GetPixel(x, i);
				if (ColorWithinTolerance(current, basecolor, tolerance) || MatchesColors(current, ignore, tolerance))
				{
					height++;
					gap = 0;
				}
				else
				{
					gap++;
					if (gap >= stopgap)
						break;
				}
			}
			gap = 0;
			for (int i = y+1; i < pic.Height; i++)
			{
				Color current = pic.GetPixel(x, i);
				if (ColorWithinTolerance(current, basecolor, tolerance) || MatchesColors(current, ignore, tolerance))
				{
					height++;
					gap = 0;
				}
				else
				{
					gap++;
					if (gap >= stopgap)
						break;
				}
			}

			return new System.Drawing.Point(width, height);
		}

		public static Bitmap GetScreen()
		{
			//Create a new bitmap.
			var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
										   Screen.PrimaryScreen.Bounds.Height,
										   PixelFormat.Format32bppArgb);

			// Create a graphics object from the bitmap.
			var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

			// Take the screenshot from the upper left corner to the right bottom corner.
			gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
										Screen.PrimaryScreen.Bounds.Y,
										0,
										0,
										Screen.PrimaryScreen.Bounds.Size,
										CopyPixelOperation.SourceCopy);
			return bmpScreenshot;
		}

		public static System.Drawing.Point GetMousePos()
		{
			System.Drawing.Point output = new System.Drawing.Point();
			GetCursorPos(ref output);
			return output;
		}

		private static bool ColorWithinTolerance(Color a, Color b, int tolerance)
		{
			return (Math.Abs(a.A - b.A) < tolerance && Math.Abs(a.R - b.R) < tolerance && Math.Abs(a.G - b.G) < tolerance && Math.Abs(a.B - b.B) < tolerance);
		}
	}

	public class PixelPoller
	{
		private Thread thread;
		private PollPixelData data;

		public delegate void EventHook(PollPixelData data);
		private EventHook PollPixelCallback;

		private ScreenReader ThisThreadScreenReader;

		public bool Repeating = false;
		public bool OneShot = true;
		public bool Inverted = false;

		private bool Running = false;
		private bool Fired = false;

		public PixelPoller(System.Drawing.Point loc, Color color, int tolerance, int delayms, bool invert = false, bool repeating = false, bool oneshot = true)
		{
			data = new PollPixelData();
			data.Location = loc;
			data.PollColor = color;
			data.PollDelay = delayms;
			data.Tolerance = tolerance;

			ThisThreadScreenReader = new ScreenReader();
			thread = new Thread(new ThreadStart(() => { PollPixel(); }));
			thread.IsBackground = true;
			thread.Name = "Pixel Poller Thread [" + loc.X + "," + loc.Y + "] (" + color.R + "," + color.G + "," + color.B + ")";

			Repeating = repeating;
			OneShot = oneshot;
			Inverted = invert;
		}

		[STAThread]
		public void Start()
		{
			if (Running) return;
			Running = true;
			thread.Start();
		}

		public void Stop()
		{
			if (Running)
			try { thread.Abort(); }
			catch (Exception ex) { }
			finally { Running = false; }
		}

		public bool IsRunning()
		{
			return Running;
		}

		public void HookEvent(EventHook e)
		{
			PollPixelCallback += e;
		}

		public void UnHookEvent(EventHook e)
		{
			PollPixelCallback -= e;
		}

		private void PollPixel()
		{
			while (true)
			{
				var c = ThisThreadScreenReader.GetColorAtPoint(data.Location);

				if ((!Inverted && ColorWithinTolerance(c, data.PollColor, data.Tolerance)) || (Inverted && !ColorWithinTolerance(c, data.PollColor, data.Tolerance)))
				{
					if (!Repeating || (Repeating && !Fired))
					{
						data.FoundColor = c;
						data.Inverted = Inverted;

						PollPixelCallback(data);

						if (OneShot)
							Fired = true;

						if (!Repeating)
						{
							Running = false;
							return;
						}
					}
				}
				else
				{
					Fired = false;
				}

				Thread.Sleep(data.PollDelay);
			}
		}

		private bool ColorWithinTolerance(Color a, Color b, int tolerance)
		{
			return (Math.Abs(a.A - b.A) < tolerance && Math.Abs(a.R - b.R) < tolerance && Math.Abs(a.G - b.G) < tolerance && Math.Abs(a.B - b.B) < tolerance);
		}
	}

	public class PollPixelData
	{
		public Color PollColor;
		public Color FoundColor;
		public int PollDelay;
		public System.Drawing.Point Location;
		public int Tolerance;
		public bool Inverted;
	}

	public static class ImageFinder
	{
		public static System.Drawing.Point FindImageInScreen(Bitmap image, bool ReturnCenters = true)
		{
			return FindMatcheInImage(ScreenReader.GetScreen(), image, ReturnCenters);
		}

		public static System.Drawing.Point FindMatcheInImage(Bitmap source, Bitmap match, bool ReturnCenters = true)
		{
			//GpuInvoke.MatchTemplate(gpuImage, gpuRandomObj, gpuMatch, TM_TYPE.CV_TM_SQDIFF, IntPtr.Zero, IntPtr.Zero);

			Image<Gray, byte> Src = new Image<Gray, byte>(source);
			GpuImage<Gray, byte> GpuSrc = new GpuImage<Gray, byte>(Src);

			Image<Gray, byte> Match = new Image<Gray, byte>(match);
			GpuImage<Gray, byte> GpuMatch = new GpuImage<Gray, byte>(Match);

			GpuImage<Gray, float> ResultImg = new GpuImage<Gray, float>(GpuSrc.Size.Width, GpuSrc.Size.Height);

			GpuTemplateMatching<Gray, byte> Matcher = new GpuTemplateMatching<Gray, byte>(TM_TYPE.CV_TM_CCOEFF_NORMED, new Size(8, 8));
			Matcher.Match(GpuSrc, GpuMatch, ResultImg, null);

			double Min = 0.0d, Max = 0.0d;
			Point MinLoc = new Point(), MaxLoc = new Point();

			GpuInvoke.MinMaxLoc(ResultImg.Ptr, ref Min, ref Max, ref MinLoc, ref MaxLoc, IntPtr.Zero);

			return new System.Drawing.Point(MaxLoc.X, MaxLoc.Y);
		}

		public static Bitmap ConvertToFormat(this System.Drawing.Image image, PixelFormat format)
		{
			Bitmap copy = new Bitmap(image.Width, image.Height, format);
			using (Graphics gr = Graphics.FromImage(copy))
			{
				gr.DrawImage(image, new Rectangle(0, 0, copy.Width, copy.Height));
			}
			return copy;
		}
	}

	public class KeyListener
	{

	}

	public class ClipboardAsync
	{

		private string _GetText;
		private void _thGetText(object format)
		{
			try
			{
				if (format == null)
				{
					_GetText = Clipboard.GetText();
				}
				else
				{
					_GetText = Clipboard.GetText((TextDataFormat)format);

				}
			}
			catch (Exception ex)
			{
				//Throw ex 
				_GetText = string.Empty;
			}
		}
		public string GetText()
		{
			ClipboardAsync instance = new ClipboardAsync();
			Thread staThread = new Thread(instance._thGetText);
			staThread.SetApartmentState(ApartmentState.STA);
			staThread.Start();
			staThread.Join();
			return instance._GetText;
		}
		public string GetText(TextDataFormat format)
		{
			ClipboardAsync instance = new ClipboardAsync();
			Thread staThread = new Thread(instance._thGetText);
			staThread.SetApartmentState(ApartmentState.STA);
			staThread.Start(format);
			staThread.Join();
			return instance._GetText;
		}
	}
}