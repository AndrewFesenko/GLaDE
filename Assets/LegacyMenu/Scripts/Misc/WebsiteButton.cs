using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GLaDE.UI{
    public class WebsiteButton : MonoBehaviour
    {
        public void OpenSite(){
            Application.OpenURL("https://afesenko.com");
        }
    }
}