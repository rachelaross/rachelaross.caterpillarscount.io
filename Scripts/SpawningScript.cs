using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawningScript : MonoBehaviour
{

    //Lets you adjust how many bugs you want on that branch through inspector
    public int numOfDesiredBugs;

    void Start()
    {




        //keeps track of which bugs already removed
        HashSet<Transform> alreadyRemoved = new HashSet<Transform>();

        //For loop to removed excess bugs
        int removeCount = transform.childCount - numOfDesiredBugs;
        for (int i = 0; i < removeCount; i++)
        {
            //randomly picks a child to delete
            int childToRemove = Random.Range(0, transform.childCount - 1);
            Transform child = transform.GetChild(childToRemove);

            //while loop checks and see if that child has already been deleted
            while(alreadyRemoved.Contains(child))
            { 
                childToRemove = Random.Range(0, transform.childCount - 1);
                child = transform.GetChild(childToRemove);
            }

            //removes child and adds it to Hashset
            Destroy(child.gameObject);
            alreadyRemoved.Add(child);


        }

    }

  
}
