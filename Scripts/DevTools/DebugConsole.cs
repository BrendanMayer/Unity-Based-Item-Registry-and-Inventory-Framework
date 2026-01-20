using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DebugConsole : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Inventory inventory;

    [Header("ItemList")]
    [SerializeField] private List<ItemData> itemList = new List<ItemData>();

    bool visible;
    private string lastInput = "";

    // Return true on success, false on failure (keeps console open on failure)
    Dictionary<string, Func<string[], bool>> commands = new Dictionary<string, Func<string[], bool>>();

    void Awake()
    {
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(_ => TryExecuteFromField());
            inputField.onEndEdit.AddListener(text =>
            {
                if (PlayerInput.instance.PerformedConsoleSubmit())
                    TryExecuteFromField();
            });
        }

        RegisterCommand("echo", args =>
        {
            Log(string.Join(" ", args));
            return true;
        });

        RegisterCommand("setwalkspeed", args =>
        {
            if (playerController == null || args.Length < 1) { Log("Usage: setwalkspeed <value|reset>"); return false; }

            try
            {
                if (args[0].ToLower() == "reset")
                {
                    playerController.walkSpeed = playerController.GetDefaultWalkSpeed();
                    Log($"Player walk speed reset to default {playerController.GetDefaultWalkSpeed()}");
                    return true;
                }

                float speed = float.Parse(args[0]);
                playerController.walkSpeed = speed;
                Log($"Player walk speed set to {speed}");
                return true;
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); return false; }
        });

        RegisterCommand("setrunspeed", args =>
        {
            if (playerController == null || args.Length < 1) { Log("Usage: setrunspeed <value|reset>"); return false; }

            try
            {
                if (args[0].ToLower() == "reset")
                {
                    playerController.runSpeed = playerController.GetDefaultRunSpeed();
                    Log($"Player run speed reset to default {playerController.GetDefaultRunSpeed()}");
                    return true;
                }

                float speed = float.Parse(args[0]);
                playerController.runSpeed = speed;
                Log($"Player run speed set to {speed}");
                return true;
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); return false; }
        });

        RegisterCommand("setgravity", args =>
        {
            if (playerController == null || args.Length < 1) { Log("Usage: setgravity <value|reset>"); return false; }
            try
            {
                if (args[0].ToLower() == "reset")
                {
                    playerController.gravity = playerController.GetDefaultGravity();
                    Log($"Player gravity reset to default {playerController.GetDefaultGravity()}");
                    return true;
                }
                float gravity = float.Parse(args[0]);
                playerController.gravity = gravity;
                Log($"Player gravity set to {gravity}");
                return true;
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); return false; }
        });

        RegisterCommand("setjumpheight", args =>
        {
            if (playerController == null || args.Length < 1) { Log("Usage: setjumpheight <value|reset>"); return false; }
            try
            {
                if (args[0].ToLower() == "reset")
                {
                    playerController.jumpHeight = playerController.GetDefaultJumpHeight();
                    Log($"Player jump height reset to default {playerController.GetDefaultJumpHeight()}");
                    return true;
                }
                float height = float.Parse(args[0]);
                playerController.jumpHeight = height;
                Log($"Player jump height set to {height}");
                return true;
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); return false; }
        });

        RegisterCommand("giveitem", args =>
        {
            if (inventory == null || itemList == null || itemList.Count == 0 || args.Length < 1)
            {
                Log("Usage: giveitem <itemid> [amount]");
                return false;
            }

            // Parse item id
            if (!int.TryParse(args[0], out int itemID))
            {
                Log("Invalid item id. Usage: giveitem <itemid> [amount]");
                return false;
            }

            // Parse amount (default 1)
            int amount = 1;
            if (args.Length >= 2 && (!int.TryParse(args[1], out amount) || amount <= 0))
            {
                Log("Invalid amount. Usage: giveitem <itemid> [amount]");
                return false;
            }

            var itemData = itemList.Find(i => i.id == itemID);
            if (itemData == null)
            {
                Log($"Item not found: {itemID}");
                return false;
            }

            // Add the whole stack at once
            var stack = new Item(itemData);
            stack.SetAmount(amount);

            int leftover = inventory.AddItem(stack);   // Inventory.AddItem already updates the UI
            int added = amount - leftover;

            if (added <= 0)
            {
                Log($"Inventory full. Could not add {itemData.itemName}.");
                return false;
            }
            else if (leftover > 0)
            {
                Log($"Added {added} of {amount} x {itemData.itemName}. Inventory is full.");
                return true;
            }
            else
            {
                Log($"Added {amount} x {itemData.itemName} to inventory.");
                return true;
            }
        });

        RegisterCommand("last", args =>
        {
            Log(string.Join(lastInput + " ", args));
            return true;
        });

    }

    

    void RegisterCommand(string name, Func<string[], bool> func)
    {
        commands[name.ToLower()] = func;

    }

    void Update()
    {
        if (PlayerInput.instance.PerformedConsoleToggle())
            SetConsoleVisible(!visible);
    }

    // Centralized show/hide logic
    void SetConsoleVisible(bool show)
    {
        visible = show;

        PlayerInput.instance.ToggleInputMapToConsole(visible);
        UIManager.instance.ToggleConsoleWindow(visible);

        if (visible)
        {
            inputField.text = "";
            inputField.gameObject.SetActive(true);
            inputField.ActivateInputField();
        }
        else
        {
            inputField.DeactivateInputField();
            inputField.gameObject.SetActive(false);
        }
    }

    // Called by input field submit/end-edit
    void TryExecuteFromField()
    {
        string line = inputField.text;
        lastInput = line;
        bool success = Execute(line);
        
        // Clear either way
        inputField.text = "";

        // Close console only on success
        if (success)
            SetConsoleVisible(false);
        else
            inputField.ActivateInputField(); // keep focus so user can fix & resend
    }

    // Returns true if a command ran successfully (used to decide closing)
    bool Execute(string line)
    {
        if (line == "last")
            line = lastInput;
        if (string.IsNullOrWhiteSpace(line)) return false;

        string[] parts = line.Split(' ');
        string cmd = parts[0].ToLower();
        string[] args = new string[Mathf.Max(0, parts.Length - 1)];
        Array.Copy(parts, 1, args, 0, args.Length);

        if (commands.TryGetValue(cmd, out var func))
        {
            try { return func(args); }
            catch (Exception ex) { Log($"Error: {ex.Message}"); return false; }
        }
        else
        {
            Log($"Unknown command: {cmd}");
            return false;
        }
    }

    void Log(string msg)
    {
        Debug.Log($"[Console] {msg}");
    }

    private void setLastInput(string line)
    {
        lastInput = line;
    }
}
