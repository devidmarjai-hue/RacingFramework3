using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PhotonView))]
public class PlayerMove : MonoBehaviourPun
{
    public float moveSpeed = 5f;
    public float rotateSpeed = 180f;

    private PhotonView pv;
    private Rigidbody rb;

    // Start pozíció tárolása
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Start()
    {
        pv = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody>();

        startPosition = transform.position;
        startRotation = transform.rotation;

        // Jelzés, hogy ki a helyi játékos
        if (!pv.IsMine)
            GetComponent<MeshRenderer>().material.color = Color.red;
        else
            GetComponent<MeshRenderer>().material.color = Color.green;
    }

    void Update()
    {
        if (!pv.IsMine) return;

        HandleMovement();
        HandleLocateInput();
    }

    private void HandleMovement()
    {
        Vector2 moveInput = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
        }

        Vector3 move = transform.forward * moveInput.y * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);

        if (moveInput.x != 0)
            transform.Rotate(Vector3.up, moveInput.x * rotateSpeed * Time.deltaTime);
    }

    private void HandleLocateInput()
    {
        // Példa: R lenyomására vissza a start pozícióra
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            LocatePlayer(startPosition, startRotation);
        }
    }

    /// <summary>
    /// Univerzális függvény a játékos mozgatására bármely pozícióba és rotációba.
    /// </summary>
    public void LocatePlayer(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
