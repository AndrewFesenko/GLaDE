using UnityEngine;

namespace GLaDE.UI{
	[CreateAssetMenu(menuName = "ThemeSettings")]
	[System.Serializable]
	public class ThemedUIData : ScriptableObject {
		[System.Serializable]
		public class Custom1{
			[Header("Nickname")]	
			public string name;
			[Header("Text")]	
			public Color graphic1;
			public Color32 text1;
		}

		[System.Serializable]
		public class Custom2{
			[Header("Nickname")]	
			public string name;
			[Header("Text")]	
			public Color graphic2;
			public Color32 text2;
		}

		[System.Serializable]
		public class Custom3{
			[Header("Nickname")]	
			public string name;
			[Header("Text")]	
			public Color graphic3;
			public Color32 text3;
		}

		[Header("CUSTOM SETTINGS")]
		public Custom1 custom1;
		public Custom2 custom2;
		public Custom3 custom3;

		[HideInInspector]
		public Color currentColor;
		[HideInInspector]
		public Color32 textColor;
	}
}