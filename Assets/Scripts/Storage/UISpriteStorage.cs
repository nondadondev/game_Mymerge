using System;
using UnityEngine;
using UnityEngine.UI;

public enum UIColor
{
    Red,
    Orange,
    Yellow,
    Green,
    Blue,
    Purple,
    White,
    Gray,
    Black,
    Gold,
    Silver,
    Bronze,
    Brown,
    Mint,
    SkyBlue,
    Pink
}

public enum UIIcon
{
    
}

public class UISpriteStorage : MonoBehaviour
{
    public static UISpriteStorage i;

    private void Awake()
    {
        i = this;
    }
    
    public Sprite btn_Red;
    public Sprite btn_Orange;
    public Sprite btn_Yellow;
    public Sprite btn_Green;
    public Sprite btn_Blue;
    public Sprite btn_Purple;
    public Sprite btn_White;
    public Sprite btn_Gray;
    public Sprite btn_Black;
    public Sprite btn_Gold;
    public Sprite btn_Silver;
    public Sprite btn_Bronze;
    public Sprite btn_Brown;
    public Sprite btn_Mint;
    public Sprite btn_SkyBlue;
    public Sprite btn_Pink;

    public Sprite GetUIBtn(UIColor color)
    {
        switch (color)
        {
            default: return btn_White;
            case UIColor.Red: return btn_Red;
            case UIColor.Orange: return btn_Orange;
            case UIColor.Yellow: return btn_Yellow;
            case UIColor.Green: return btn_Green;
            case UIColor.Blue: return btn_Blue;
            case UIColor.Purple: return btn_Purple;
            case UIColor.White: return btn_White;
            case UIColor.Gray: return btn_Gray;
            case UIColor.Black: return btn_Black;
            case UIColor.Gold: return btn_Gold;
            case UIColor.Silver: return btn_Silver;
            case UIColor.Bronze: return btn_Bronze;
            case UIColor.Brown: return btn_Brown;
            case UIColor.Mint: return btn_Mint;
            case UIColor.SkyBlue: return btn_SkyBlue;
            case UIColor.Pink: return btn_Pink;
        }
    }
    
    public Sprite pan_Red;
    public Sprite pan_Orange;
    public Sprite pan_Yellow;
    public Sprite pan_Green;
    public Sprite pan_Blue;
    public Sprite pan_Purple;
    public Sprite pan_White;
    public Sprite pan_Gray;
    public Sprite pan_Black;
    public Sprite pan_Gold;
    public Sprite pan_Silver;
    public Sprite pan_Bronze;
    public Sprite pan_Brown;
    public Sprite pan_Mint;
    public Sprite pan_SkyBlue;
    public Sprite pan_Pink;

    public Sprite GetUIPan(UIColor color)
    {
        switch (color)
        {
            default: return pan_White;
            case UIColor.Red: return pan_Red;
            case UIColor.Orange: return pan_Orange;
            case UIColor.Yellow: return pan_Yellow;
            case UIColor.Green: return pan_Green;
            case UIColor.Blue: return pan_Blue;
            case UIColor.Purple: return pan_Purple;
            case UIColor.White: return pan_White;
            case UIColor.Gray: return pan_Gray;
            case UIColor.Black: return pan_Black;
            case UIColor.Gold: return pan_Gold;
            case UIColor.Silver: return pan_Silver;
            case UIColor.Bronze: return pan_Bronze;
            case UIColor.Brown: return pan_Brown;
            case UIColor.Mint: return pan_Mint;
            case UIColor.SkyBlue: return pan_SkyBlue;
            case UIColor.Pink: return pan_Pink;
        }
    }

    public Sprite title_Red;
    public Sprite title_Orange;
    public Sprite title_Yellow;
    public Sprite title_Green;
    public Sprite title_Blue;
    public Sprite title_Purple;
    public Sprite title_White;
    public Sprite title_Gray;
    public Sprite title_Black;
    public Sprite title_Gold;
    public Sprite title_Silver;
    public Sprite title_Bronze;
    public Sprite title_Brown;
    public Sprite title_Mint;
    public Sprite title_SkyBlue;

    public Sprite GetUITitle(UIColor color)
    {
        switch (color)
        {
            default: return title_White;
            case UIColor.Red: return title_Red;
            case UIColor.Orange: return title_Orange;
            case UIColor.Yellow: return title_Yellow;
            case UIColor.Green: return title_Green;
            case UIColor.Blue: return title_Blue;
            case UIColor.Purple: return title_Purple;
            case UIColor.White: return title_White;
            case UIColor.Gray: return title_Gray;
            case UIColor.Black: return title_Black;
            case UIColor.Gold: return title_Gold;
            case UIColor.Silver: return title_Silver;
            case UIColor.Bronze: return title_Bronze;
            case UIColor.Brown: return title_Brown;
            case UIColor.Mint: return title_Mint;
            case UIColor.SkyBlue: return title_SkyBlue;
        }
    }
}