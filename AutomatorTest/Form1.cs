using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Automator;

namespace AutomatorTest
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Thread.Sleep(1000);

			//System.Drawing.Point pt = ScreenReader.GetSizeOfColorRegion(ScreenReader.GetMousePos().X, ScreenReader.GetMousePos().Y, 5, new Color[] { Color.Black }, 0);
			//Console.WriteLine("Area = [" + pt.X + ", " + pt.Y + "]");

			PixelPoller p = new PixelPoller(new System.Drawing.Point(1526,904), Color.FromArgb(199, 237, 252), 5, 150, false, true, true);
			p.HookEvent(DoEvent);
			p.Start();
		}

		private void DoEvent(PollPixelData data)
		{
			System.Drawing.Point p = ScreenReader.GetSizeOfColorRegion(1526, 904, 5, new Color[] { Color.Black });
			MouseController.ClickAndDrag(1761, 900, 1530, 900 - p.Y - 50);
			KeyboardController.TypeModifiedKey(AutomatorKeyCode.LCONTROL, AutomatorKeyCode.VK_C);
			MouseController.Wait(200);
			MouseController.ClickPoint(1616, 997);
			KeyboardController.TypeModifiedKey(AutomatorKeyCode.LCONTROL, AutomatorKeyCode.VK_V);
			MouseController.Wait(200);
			KeyboardController.TypeStringDelay(" - why would you say that to me, I'm just a robot", 5);
			MouseController.Wait(200);
			KeyboardController.TypeKey(AutomatorKeyCode.RETURN);
		}
	}
}
