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
- **config/spook_ai_config.yaml** - Sample ML-Agents training configuration

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