using UnityEngine;
using UnityEditor;

namespace GLaDEUI{
	public class GLaDEUIWindow : EditorWindow {

		[MenuItem("Window/GLaDEUI/Online Documentation")]
		public static void ShowWindow(){
			Application.OpenURL("https://www.GLaDEUI.com/documentation");
		}
	}
}
