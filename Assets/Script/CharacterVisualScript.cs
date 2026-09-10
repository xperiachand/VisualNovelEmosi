using UnityEngine;
using Yarn.Unity;

public class CharacterVisualScript : MonoBehaviour
{
    public SpriteRenderer reyDummy;

    [YarnCommand("show_tense")]
    public void ShowTense()
    {
        reyDummy.color = Color.yellow; 
    }

    [YarnCommand("show_normal")]
    public void ShowNormal()
    {
        reyDummy.color = Color.white;
    }

    [YarnCommand("set_expression")]
    public void SetExpression(string emotion)
    {

    }

}
