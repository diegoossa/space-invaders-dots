using Unity.Entities;
using UnityEngine;

public class InputSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        var horizontal = Input.GetAxis("Horizontal");
        Entities.ForEach((ref PlayerInput input) => { input.Horizontal = horizontal; });
    }
}