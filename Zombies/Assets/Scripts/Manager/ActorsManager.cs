using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActorsManager : MonoBehaviour
{
    public List<Actor> actors = new List<Actor>();

    private void Awake()
    {
        actors = new List<Actor>();
    }
}
