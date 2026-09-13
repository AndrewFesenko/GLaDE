using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GLaDE.UI{
    public class UIThemeManager : MonoBehaviour
    {
        public enum Theme { custom1, custom2, custom3 };

        [Header("Theme Settings")]
        public Theme theme;
        int themeIndex;
        public ThemedUIData themeController;

        void Start(){
            SetThemeColors();
        }

        void SetThemeColors(){
			if (theme == Theme.custom1){
				themeController.currentColor = themeController.custom1.graphic1;
				themeController.textColor = themeController.custom1.text1;
				themeIndex = 0;
			}else if (theme == Theme.custom2){
				themeController.currentColor = themeController.custom2.graphic2;
				themeController.textColor = themeController.custom2.text2;
				themeIndex = 1;
			}else if (theme == Theme.custom3){
				themeController.currentColor = themeController.custom3.graphic3;
				themeController.textColor = themeController.custom3.text3;
				themeIndex = 2;
			}
		}

        public void ChangeTheme(int index){
            if(index == 0){
                theme = Theme.custom1;
            }else if(index == 1){
                theme = Theme.custom2;
            }else if(index == 2){
                theme = Theme.custom3;
            }

            SetThemeColors();
        }
    }
}