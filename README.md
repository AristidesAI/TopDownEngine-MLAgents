# ML-Agents/TopDownEngine Integration Guide

This guide explains how to integrate the ML-Agents adapter with your TopDownEngine project.

## Overview

The ML-Agents adapter provides a bridge between Unity ML-Agents and TopDownEngine's AI system, allowing you to train AI agents using reinforcement learning while leveraging TopDownEngine's character controllers and abilities.

## Components

### 1. MLAgentsAIBrain
**File:** `MLAgentsAIBrain.cs`

The core adapter that extends ML-Agents' `Agent` class and integrates with TopDownEngine components.

**Key Features:**
- Automatic component discovery (Character, TopDownController, etc.)
- Configurable observations (position, velocity, threats, walls)
- Action mapping to TopDownEngine movement system
- Reward system aligned with game objectives
- Memory system for last known player position

### 2. MLAgentsTrainingArea
**File:** `MLAgentsTrainingArea.cs`

Manages the training environment, episodes, and spawn points.

**Key Features:**
- Episode management with configurable duration
- Automatic maze regeneration
- Spawn point management
- Training metrics tracking

### 3. MLAgentsConeOfVisionSensor
**File:** `MLAgentsConeOfVisionSensor.cs`

Custom sensor that integrates TopDownEngine's ConeOfVision with ML-Agents observations.

**Key Features:**
- Raycast-based vision within cone
- Target type detection (player, enemy, obstacle)
- Velocity observations for moving targets

### 4. MLAgentsRuntimeInference
**File:** `MLAgentsRuntimeInference.cs`

Runtime inference using Unity Sentis for deployed models.

**Key Features:**
- ONNX model loading
- Multiple backend support (CPU, GPU, etc.)
- Model hot-swapping

## Integration Steps

### Step 1: Copy Files to Your Project

1. Copy all files from the `ML-TD` folder to your Unity project's `Assets/Scripts/MLAgents/` directory
2. Ensure the namespace `testNet.MLAgents` doesn't conflict with your project

### Step 2: Add Required Packages

In Unity Package Manager, add:
```
- com.unity.ml-agents (2.0+)
- com.unity.sentis (latest)
- TopDownEngine (already installed)
```

### Step 3: Create Assembly Definition (Optional)

Create an assembly definition file to manage dependencies:

**File:** `testNet.MLAgents.asmdef`
```json
{
    "name": "testNet.MLAgents",
    "rootNamespace": "testNet.MLAgents",
    "references": [
        "Unity.MLAgents",
        "Unity.MLAgents.Policies",
        "Unity.Sentis",
        "MoreMountains.TopDownEngine",
        "MoreMountains.Tools"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

### Step 4: Set Up AI Character Prefab

1. **Duplicate an existing AI character** (e.g., KoalaNinjaPatrol)
2. **Add ML-Agents components:**
   - `MLAgentsAIBrain`
   - `BehaviorParameters`
   - `DecisionRequester`
   - `MLAgentsConeOfVisionSensor` (if using visual observations)

3. **Configure MLAgentsAIBrain:**
   ```
   - Linked AI Brain: (Auto-detected)
   - Linked Character: (Auto-detected)
   - Linked Controller: (Auto-detected)
   - Wall Layer Mask: Obstacles, Walls
   - Player Transform: (Assign player GameObject)
   - Threat Distance: 10
   - Danger Distance: 3
   ```

4. **Configure BehaviorParameters:**
   ```
   - Behavior Name: "TestAI"
   - Vector Observation Space Size: 
     - Base: 4 (position + velocity)
     - Threat: 9 (if enabled)
     - Walls: 8 (number of rays)
     - Total: ~21
   - Continuous Actions: 2 (movement X, Y)
   - Discrete Actions: 1 branch, size 3 (speed modes)
   ```

5. **Configure DecisionRequester:**
   ```
   - Decision Period: 5
   - Take Actions Between Decisions: true
   ```

### Step 5: Set Up Training Scene

1. **Create a new scene** or duplicate existing
2. **Add MLAgentsTrainingArea** to an empty GameObject
3. **Configure Training Area:**
   ```
   - Max Episode Length: 180 (3 minutes)
   - Regenerate Maze On Reset: true
   - Min Spawn Distance: 5
   - Player Character: (Assign player)
   - AI Agents: (Add all AI characters)
   ```

### Step 6: Create Training Configuration

Create a YAML config file for ML-Agents training:

**File:** `config/Test_ai_config.yaml`
```yaml
behaviors:
  TestAI:
    trainer_type: ppo
    hyperparameters:
      batch_size: 1024
      buffer_size: 10240
      learning_rate: 3.0e-4
      beta: 5.0e-3
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
      learning_rate_schedule: linear
    network_settings:
      normalize: true
      hidden_units: 512
      num_layers: 3
      vis_encode_type: simple
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
      curiosity:
        strength: 0.02
        gamma: 0.99
        encoding_size: 256
        learning_rate: 3.0e-4
    max_steps: 5000000
    time_horizon: 128
    summary_freq: 30000
```

### Step 7: Layer Configuration

Ensure your project has proper layers:
```
- Player (Layer 10)
- Obstacles (Layer 8)
- Enemy (Layer 13)
- World (Layer 9)
```

### Step 8: Training Setup

1. **Install ML-Agents Python package:**
   ```bash
   pip install mlagents==0.30.0
   ```

2. **Start training:**
   ```bash
   mlagents-learn config/Test_ai_config.yaml --run-id=testAI_001
   ```

3. **Press Play in Unity** when prompted

## Manual Implementation Requirements

### 1. Custom AIAction for ML-Agents

Create a TopDownEngine AIAction that uses ML-Agents decisions:

```csharp
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;
using UnityEngine;

[AddComponentMenu("TopDown Engine/Character/AI/Actions/AI Action ML Agents")]
public class AIActionMLAgents : AIAction
{
    protected MLAgentsAIBrain _mlAgent;
    
    public override void Initialization()
    {
        base.Initialization();
        _mlAgent = GetComponentInParent<MLAgentsAIBrain>();
    }
    
    public override void PerformAction()
    {
        // ML-Agents handles movement through OnActionReceived
        // This action just ensures the AI system doesn't interfere
    }
}
```

### 2. Tagging System Integration

Implement the tagging ability as described in the main plan. The MLAgentsAIBrain already has `OnTagged()` method ready for integration.

### 3. Maze Generator Setup

Use TopDownEngine's `TilemapLevelGenerator` with custom generation:
- Set Generation Method to "Random Walk" or custom algorithm
- Configure bounds and spawn areas
- Add to MLAgentsTrainingArea reference

### 4. Model Export and Deployment

After training:
1. Export the model as ONNX
2. Import into Unity as ModelAsset
3. Add MLAgentsRuntimeInference component for deployment
4. Configure backend for target platform (iOS = CoreML)

## Troubleshooting

### Common Issues:

1. **"Missing TopDownEngine components"**
   - Ensure character has Character, TopDownController components
   - Check parent-child hierarchy

2. **"Observations don't match"**
   - Verify BehaviorParameters observation size matches actual observations
   - Check all observation flags in MLAgentsAIBrain

3. **"Agent doesn't move"**
   - Verify TopDownController is receiving input
   - Check movement speed multipliers
   - Ensure character isn't stuck in walls

4. **"Training doesn't converge"**
   - Adjust reward values
   - Increase training steps
   - Check if environment is too difficult initially

## Performance Optimization

1. **Reduce observation frequency:**
   - Increase DecisionRequester period
   - Disable unused observations

2. **Optimize sensors:**
   - Reduce ray count for wall detection
   - Limit ConeOfVision rays

3. **Mobile optimization:**
   - Use Sentis with CoreML backend for iOS
   - Reduce model complexity
   - Enable model quantization

## Next Steps

1. Implement remaining game systems (maze generation, tagging, UI)
2. Train initial model with heuristic behavior
3. Iterate on reward function based on gameplay
4. Test deployment with Sentis
5. Optimize for target platforms

## Additional Resources

- [ML-Agents Documentation](https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Readme.md)
- [Unity Sentis Documentation](https://docs.unity3d.com/Packages/com.unity.sentis@latest)
- [TopDownEngine Documentation](https://topdown-engine-docs.moremountains.com/) 


# ML-Agents/TopDownEngine Adapter

This folder contains the ML-Agents adapter for TopDownEngine, allowing you to train AI agents using reinforcement learning while leveraging TopDownEngine's character control system.

## Contents

- **MLAgentsAIBrain.cs** - Core adapter that bridges ML-Agents with TopDownEngine
- **MLAgentsTrainingArea.cs** - Training environment manager
- **MLAgentsConeOfVisionSensor.cs** - Custom visual sensor using TopDownEngine's ConeOfVision
- **MLAgentsRuntimeInference.cs** - Runtime inference using Unity Sentis
- **AIActionMLAgents.cs** - TopDownEngine AIAction for ML-Agents control
- **ReadOnlyAttribute.cs** - Helper attribute for inspector fields
- **INTEGRATION_GUIDE.md** - Detailed integration instructions
- **config/_ai_config.yaml** - Sample ML-Agents training configuration

## Quick Start

1. Copy this folder to your Unity project's Assets directory
2. Install required packages: ML-Agents, Unity Sentis
3. Follow the INTEGRATION_GUIDE.md for detailed setup
4. Attach MLAgentsAIBrain to your AI characters
5. Configure BehaviorParameters and DecisionRequester
6. Start training with: `mlagents-learn config/spook_ai_config.yaml`

## Key Features

- **Seamless Integration**: Works with existing TopDownEngine characters
- **Flexible Observations**: Position, velocity, threats, walls, and vision
- **Smart Actions**: Movement with variable speed based on threat level
- **Memory System**: AI remembers last known player position
- **Training Management**: Automatic episode resets and maze regeneration
- **Runtime Inference**: Deploy trained models using Unity Sentis

## Architecture

```
TopDownEngine Character
├── MLAgentsAIBrain (ML-Agents Agent)
├── BehaviorParameters
├── DecisionRequester
├── MLAgentsConeOfVisionSensor (optional)
└── TopDownEngine Components
    ├── Character
    ├── TopDownController
    ├── CharacterMovement
    └── ConeOfVision2D
```

## Training Workflow

1. **Setup**: Configure AI prefabs with ML-Agents components
2. **Environment**: Use MLAgentsTrainingArea for episode management
3. **Train**: Run ML-Agents training with provided config
4. **Test**: Validate behavior in Unity
5. **Deploy**: Use MLAgentsRuntimeInference with Sentis

## Notes

- The adapter is designed for 2D top-down games
- Supports curriculum learning for progressive difficulty
- Compatible with iOS deployment via Unity Sentis
- Includes self-play support for adversarial training

For detailed instructions, see INTEGRATION_GUIDE.md 
