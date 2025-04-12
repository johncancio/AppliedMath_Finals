using System;
using System.Collections.Generic;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;
using UnityEngine.UI;
using TMPro;


// Enhanced MeshGenerator with collision, player control, and camera following
public class EnhancedMeshGenerator : MonoBehaviour
{
    public Slider healthSlider;
    public TMP_Text healthText; // Optional


    public Material material;
    public int instanceCount = 100;
    private Mesh cubeMesh;
    private List<Matrix4x4> matrices = new List<Matrix4x4>();
    private List<int> colliderIds = new List<int>();

    public float width = 1f;
    public float height = 1f;
    public float depth = 1f;

    public float movementSpeed = 5f;
    public int playerHealth = 100;
    float damageCooldown = 1.0f; // 1 second cooldown between damage
    float lastDamageTime = -Mathf.Infinity;
    public float gravity = 9.8f;

    private int playerID = -1;
    private Vector3 playerVelocity = Vector3.zero;
    private bool isGrounded = false;

    public float jumpForce = 10f;
    public float fallMultiplier = 2f;
    private bool jumpRequested = false;

    // Camera reference
    public PlayerCameraFollow cameraFollow;

    // Z-position constant for all boxes
    public float constantZPosition = 0f;

    // Range for random generation
    public float minX = -50f;
    public float maxX = 50f;
    public float minY = -50f;
    public float maxY = 50f;

    // Ground plane settings
    public float groundY = -20f;
    public float groundWidth = 200f;
    public float groundDepth = 200f;

    // Enemy settings
    public int enemyCount = 4;
    public float enemySpeed = 2f;
    public Vector2 enemyMovementRange = new Vector2(-5f, 5f);

    private List<Vector3> enemyPositions = new List<Vector3>();
    private List<Vector3> enemyDirections = new List<Vector3>();
    private List<int> enemyIDs = new List<int>();

    // Obstacle Settings
    public int obstacleCount = 4;
    public float obstacleSizeMin = 1f;
    public float obstacleSizeMax = 3f;

    public Vector3 goalSize = new Vector3(2f, 2f, 2f);
    public Vector3 goalPosition = new Vector3(200f, 1f, 0f);
    private int goalColliderID = -1; // Initialize to -1

    private List<int> obstacleIDs = new List<int>();

    void Start()
    {
        // Find or create camera if not assigned
        SetupCamera();

        // Create the cube mesh
        CreateCubeMesh();

        // Create player box
        CreatePlayer();

        // Create ground
        CreateGround();

        // Set up random boxes
        GenerateRandomBoxes();

        // Create enemies
        GenerateEnemies();

        // Generate obstacles
        GenerateObstacles();

        // Generate goal
        GenerateGoal();

        if (healthSlider != null)
            healthSlider.value = playerHealth;

        if (healthText != null)
            healthText.text = playerHealth.ToString();

        Debug.Log($"Player Collider ID on Start: {playerID}"); // Debugging
        Debug.Log($"Goal Collider ID on Start: {goalColliderID}");   // Debugging
    }

    void UpdateHealthUI()
    {
        if (healthSlider != null)
            healthSlider.value = playerHealth;

        if (healthText != null)
            healthText.text = playerHealth.ToString();
    }


    void SetupCamera()
    {
        if (cameraFollow == null)
        {
            // Try to find existing camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // Check if it already has our script
                cameraFollow = mainCamera.GetComponent<PlayerCameraFollow>();
                if (cameraFollow == null)
                {
                    // Add our script to existing camera
                    cameraFollow = mainCamera.gameObject.AddComponent<PlayerCameraFollow>();
                }
            }
            else
            {
                // No main camera found, create a new one
                GameObject cameraObj = new GameObject("PlayerCamera");
                Camera cam = cameraObj.AddComponent<Camera>();
                cameraFollow = cameraObj.AddComponent<PlayerCameraFollow>();

                // Set this as the main camera
                cam.tag = "MainCamera";
            }

            // Configure default camera settings
            cameraFollow.offset = new Vector3(0, 0, -15);
            cameraFollow.smoothSpeed = 0.1f;
        }
    }

    void CreateCubeMesh()
    {
        cubeMesh = new Mesh();

        // Create 8 vertices for the cube (corners)
        Vector3[] vertices = new Vector3[8]
        {
            // Bottom face vertices
            new Vector3(0, 0, 0),       // Bottom front left - 0
            new Vector3(width, 0, 0),   // Bottom front right - 1
            new Vector3(width, 0, depth),// Bottom back right - 2
            new Vector3(0, 0, depth),   // Bottom back left - 3

            // Top face vertices
            new Vector3(0, height, 0),       // Top front left - 4
            new Vector3(width, height, 0),   // Top front right - 5
            new Vector3(width, height, depth),// Top back right - 6
            new Vector3(0, height, depth)    // Top back left - 7
        };

        // Triangles for the 6 faces (2 triangles per face)
        int[] triangles = new int[36]
        {
            // Front face triangles (facing -Z)
            0, 4, 1,
            1, 4, 5,

            // Back face triangles (facing +Z)
            2, 6, 3,
            3, 6, 7,

            // Left face triangles (facing -X)
            0, 3, 4,
            4, 3, 7,

            // Right face triangles (facing +X)
            1, 5, 2,
            2, 5, 6,

            // Bottom face triangles (facing -Y)
            0, 1, 3,
            3, 1, 2,

            // Top face triangles (facing +Y)
            4, 7, 5,
            5, 7, 6
        };

        Vector2[] uvs = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            uvs[i] = new Vector2(vertices[i].x / width, vertices[i].z / depth);
        }

        cubeMesh.vertices = vertices;
        cubeMesh.triangles = triangles;
        cubeMesh.uv = uvs;
        cubeMesh.RecalculateNormals();
        cubeMesh.RecalculateBounds();
    }

    void CreatePlayer()
    {
        // Create player at a specific position
        Vector3 playerPosition = new Vector3(0, 10, constantZPosition);
        Vector3 playerScale = Vector3.one;
        Quaternion playerRotation = Quaternion.identity;

        // Register with collision system - properly handle width/height/depth
        playerID = CollisionManager.Instance.RegisterCollider(
            playerPosition,
            new Vector3(width * playerScale.x, height * playerScale.y, depth * playerScale.z),
            true);

        if (playerID == -1)
        {
            Debug.LogError("Failed to register player collider!");
            return; // Important: Exit if player registration fails
        }

        // Create transformation matrix
        Matrix4x4 playerMatrix = Matrix4x4.TRS(playerPosition, playerRotation, playerScale);
        matrices.Add(playerMatrix);
        colliderIds.Add(playerID);

        // Update the matrix in collision manager
        CollisionManager.Instance.UpdateMatrix(playerID, playerMatrix);
    }

    void CreateGround()
    {
        float tileWidth = 10f; // Size of each ground tile
        int tileCount = Mathf.CeilToInt((maxX - minX) / tileWidth);

        for (int i = 0; i < tileCount; i++)
        {
            float tileX = minX + i * tileWidth + tileWidth / 2f; // Center of tile
            Vector3 groundPosition = new Vector3(tileX, groundY, constantZPosition);
            Vector3 groundScale = new Vector3(tileWidth, 1f, groundDepth);
            Quaternion groundRotation = Quaternion.identity;

            int groundID = CollisionManager.Instance.RegisterCollider(
                groundPosition,
                new Vector3(groundScale.x, groundScale.y, groundScale.z),
                false);

            if (groundID == -1)
            {
                Debug.LogError("Failed to register ground collider!");
                continue; // Continue to the next ground tile
            }

            Matrix4x4 groundMatrix = Matrix4x4.TRS(groundPosition, groundRotation, groundScale);
            matrices.Add(groundMatrix);
            colliderIds.Add(groundID);
            CollisionManager.Instance.UpdateMatrix(groundID, groundMatrix);
        }
    }


    void GenerateRandomBoxes()
    {
        // Create random boxes (excluding player and ground)
        for (int i = 0; i < instanceCount - 2; i++)
        {
            // Random position (constant Z)
            Vector3 position = new Vector3(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY),
                constantZPosition
            );

            // Random rotation only around Z axis
            Quaternion rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            // Random non-uniform scale - different for each dimension
            Vector3 scale = new Vector3(
                Random.Range(0.5f, 3f),
                Random.Range(0.5f, 3f),
                Random.Range(0.5f, 3f)
            );

            // Register with collision system - properly handle rectangular shapes
            int id = CollisionManager.Instance.RegisterCollider(
                position,
                new Vector3(width * scale.x, height * scale.y, depth * scale.z),
                false);
            
            if (id == -1)
            {
                 Debug.LogError("Failed to register random box collider!");
                 continue;
            }

            // Create transformation matrix
            Matrix4x4 boxMatrix = Matrix4x4.TRS(position, rotation, scale);
            matrices.Add(boxMatrix);
            colliderIds.Add(id);

            // Update the matrix in collision manager
            CollisionManager.Instance.UpdateMatrix(id, boxMatrix);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            jumpRequested = true;
        }
        RenderBoxes();
    }

    private void FixedUpdate()
    {
        UpdatePlayer();
        UpdateEnemies();
        CheckGoalCollision();
    }

    void UpdatePlayer()
    {
        if (playerID == -1) return;

        // Get current player matrix
        Matrix4x4 playerMatrix = matrices[colliderIds.IndexOf(playerID)];
        DecomposeMatrix(playerMatrix, out Vector3 pos, out Quaternion rot, out Vector3 scale);

        // Reset velocity.y when grounded
        if (isGrounded)
        {
            playerVelocity.y = 0;
        }

        // Handle jump
        if (jumpRequested)
        {
            playerVelocity.y = jumpForce;
            isGrounded = false;
            jumpRequested = false;
        }
        else if (!isGrounded)
        {
            // Apply stronger gravity when falling
            if (playerVelocity.y < 0)
                playerVelocity.y -= gravity * fallMultiplier * Time.deltaTime;
            else
                playerVelocity.y -= gravity * Time.deltaTime;
        }

        // Get horizontal input
        float horizontal = 0;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1;
        if (Input.GetKey(KeyCode.D)) horizontal += 1;

        // Reduce speed if airborne
        float currentSpeed = isGrounded ? movementSpeed : movementSpeed * 0.5f;

        // Update player position based on input
        Vector3 newPos = pos;
        newPos.x += horizontal * currentSpeed * Time.deltaTime;

        // Apply horizontal movement if no collision
        if (!CheckCollisionAt(playerID, new Vector3(newPos.x, pos.y, pos.z)))
        {
            pos.x = newPos.x;
        }

        // Apply gravity/vertical movement
        newPos = pos;
        newPos.y += playerVelocity.y * Time.deltaTime;

        // Check for vertical collisions
        if (CheckCollisionAt(playerID, new Vector3(pos.x, newPos.y, pos.z)))
        {
            // We hit something below or above
            if (playerVelocity.y < 0)
            {
                // We hit something below
                isGrounded = true;
            }
            playerVelocity.y = 0;
        }
        else
        {
            // No collision, apply gravity
            pos.y = newPos.y;
            isGrounded = false;
        }

        // Update matrix
        Matrix4x4 newMatrix = Matrix4x4.TRS(pos, rot, scale);
        matrices[colliderIds.IndexOf(playerID)] = newMatrix;

        // Update collider position - properly handle rectangular shape
        CollisionManager.Instance.UpdateCollider(playerID, pos, new Vector3(width * scale.x, height * scale.y, depth * scale.z));
        CollisionManager.Instance.UpdateMatrix(playerID, newMatrix);

        // Update camera to follow player
        if (cameraFollow != null)
        {
            cameraFollow.SetPlayerPosition(pos);
        }
    }

    bool CheckCollisionAt(int id, Vector3 position)
    {
        return CollisionManager.Instance.CheckCollision(id, position, out _);
    }

    void RenderBoxes()
    {
        // Convert list to array for Graphics.DrawMeshInstanced
        Matrix4x4[] matrixArray = matrices.ToArray();

        // Draw instanced meshes in batches of 1023 (GPU limit)
        for (int i = 0; i < matrixArray.Length; i += 1023)
        {
            int batchSize = Mathf.Min(1023, matrixArray.Length - i);
            Matrix4x4[] batchMatrices = new Matrix4x4[batchSize];
            System.Array.Copy(matrixArray, i, batchMatrices, 0, batchSize);
            Graphics.DrawMeshInstanced(cubeMesh, 0, material, batchMatrices, batchSize);
        }
    }

    void DecomposeMatrix(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = matrix.GetPosition();
        rotation = matrix.rotation;
        scale = matrix.lossyScale;
    }

    // Add a new random box at runtime (can be called from button or other trigger)
    public void AddRandomBox()
    {
        Vector3 position = new Vector3(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY),
            constantZPosition
        );

        Quaternion rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        // Random non-uniform scale - different for each dimension
        Vector3 scale = new Vector3(
            Random.Range(0.5f, 3f),
            Random.Range(0.5f, 3f),
            Random.Range(0.5f, 3f)
        );

        // Register with collision system - properly handle rectangular shapes
        int id = CollisionManager.Instance.RegisterCollider(
            position,
            new Vector3(width * scale.x, height * scale.y, depth * scale.z),
            false);
        if (id == -1)
        {
            Debug.LogError("Failed to register new random box collider!");
            return;
        }

        Matrix4x4 boxMatrix = Matrix4x4.TRS(position, rotation, scale);
        matrices.Add(boxMatrix);
        colliderIds.Add(id);

        CollisionManager.Instance.UpdateMatrix(id, boxMatrix);
    }

    void GenerateEnemies()
    {
        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(minX, maxX),
                groundY + 1f, // Slightly above ground
                constantZPosition
            );

            Vector3 direction = new Vector3(
                Random.Range(enemyMovementRange.x, enemyMovementRange.y),
                0f,
                0f
            ).normalized;

            int enemyID = CollisionManager.Instance.RegisterCollider(
                position,
                new Vector3(width, height, depth),
                false);
            if (enemyID == -1)
            {
                Debug.LogError("Failed to register enemy collider!");
                continue;
            }

            Matrix4x4 enemyMatrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            matrices.Add(enemyMatrix);
            colliderIds.Add(enemyID);
            CollisionManager.Instance.UpdateMatrix(enemyID, enemyMatrix);

            enemyPositions.Add(position);
            enemyDirections.Add(direction);
            enemyIDs.Add(enemyID);
        }
    }


    void ApplyDamage(int amount)
    {
        if (Time.time - lastDamageTime < damageCooldown)
            return; // Still in cooldown, ignore damage

        playerHealth -= amount;
        lastDamageTime = Time.time;

        Debug.Log($"Player took {amount} damage! Remaining health: {playerHealth}");

        // Update the health UI
        UpdateHealthUI();

        if (playerHealth <= 0)
        {
            Debug.Log("Player is dead!");
            // Add game over or respawn logic here
        }
    }

    void UpdateEnemies()
    {
        for (int i = 0; i < enemyIDs.Count; i++)
        {
            Vector3 pos = enemyPositions[i];
            Vector3 dir = enemyDirections[i];

            // Move enemy
            pos += dir * enemySpeed * Time.deltaTime;

            // Reverse direction if out of range
            if (pos.x < minX || pos.x > maxX)
            {
                dir = -dir;
            }

            // Make enemy stand on the ground
            pos.y = groundY + height * 1f;

            enemyPositions[i] = pos;
            enemyDirections[i] = dir;

            // Update matrix and collider
            int id = enemyIDs[i];
            int matrixIndex = colliderIds.IndexOf(id);
            if (matrixIndex == -1)
            {
                Debug.LogError($"Enemy ID {id} not found in colliderIds!");
                continue; // Skip this enemy
            }

            Matrix4x4 newMatrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            matrices[matrixIndex] = newMatrix;

            CollisionManager.Instance.UpdateMatrix(id, newMatrix);
            CollisionManager.Instance.UpdateCollider(id, pos, new Vector3(width, height, depth));

            // Check collision with player
            if (playerID != -1 && CollisionManager.Instance.CheckCollisionBetween(id, playerID))
            {
                ApplyDamage(10); // Damage amount
            }
        }
    }

    void GenerateObstacles()
    {
        for (int i = 0; i < obstacleCount; i++)
        {
            float x = Random.Range(minX, maxX);
            Vector3 position = new Vector3(x, groundY + height / 2f, constantZPosition); // Half-height above ground

            Vector3 scale = Vector3.one * Random.Range(obstacleSizeMin, obstacleSizeMax);
            Quaternion rotation = Quaternion.identity;

            int id = CollisionManager.Instance.RegisterCollider(
                position,
                new Vector3(width * scale.x, height * scale.y, depth * scale.z),
                false);
            if (id == -1)
            {
                Debug.LogError("Failed to register obstacle collider!");
                continue;
            }

            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
            matrices.Add(matrix);
            colliderIds.Add(id);
            obstacleIDs.Add(id);
            CollisionManager.Instance.UpdateMatrix(id, matrix);
        }
    }

    void GenerateGoal()
    {
        Quaternion goalRotation = Quaternion.identity;

        goalColliderID = CollisionManager.Instance.RegisterCollider(
            goalPosition,
            new Vector3(goalSize.x, goalSize.y, goalSize.z),
            false
        );

        if (goalColliderID == -1)
        {
            Debug.LogError("Failed to register goal collider!");
            return; // VERY IMPORTANT:  Return after failing to register the goal.
        }

        Matrix4x4 goalMatrix = Matrix4x4.TRS(goalPosition, goalRotation, goalSize);
        matrices.Add(goalMatrix);
        colliderIds.Add(goalColliderID);
        CollisionManager.Instance.UpdateMatrix(goalColliderID, goalMatrix);
        Debug.Log($"Generated Goal. ID is : {goalColliderID}, position: {goalPosition}, size: {goalSize}");
    }

    void CheckGoalCollision()
    {
        if (playerID != -1 && goalColliderID != -1) //check if they are valid
        {
            bool collision = CollisionManager.Instance.CheckCollisionBetween(playerID, goalColliderID);
            if (collision)
            {
                Debug.Log("You reached the goal! You win!");
                Time.timeScale = 0f;
                // TODO: Add win logic here: disable input, show win screen, etc.
            }
        }
        else
        {
            if(playerID == -1)
                Debug.Log("CheckGoalCollision: playerID is invalid");
            if(goalColliderID == -1)
                Debug.Log("CheckGoalCollision: goalColliderID is invalid.");
        }
    }
}