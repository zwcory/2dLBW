# LBW Predictor — Neural Network Cricket Decision System

A 2D cricket LBW (Leg Before Wicket) decision system built in Unity, using a PyTorch neural network trained on simulated ball-tracking data and deployed in-engine via ONNX and Unity Barracuda.

Developed as part of a 52 projects in 52 weeks challenge — week 4.

---

## What Is This?

In cricket, an LBW dismissal occurs when the ball hits the batter's pad and would have gone on to hit the stumps. In real cricket, ball-tracking technology (like Hawk-Eye) predicts this trajectory to assist umpires.

This project recreates that concept in a 2D Unity simulation, training a neural network on thousands of simulated deliveries and deploying it to make real-time LBW predictions in-game.

---

## How It Works

### Data Collection (Unity)
- Balls are automatically bowled at randomised angles and spin types using `FastDataCollector`
- Multiple data points are recorded per delivery as the ball travels toward the pad
- Each sample captures: ball position, velocity, angular velocity, spin type, distance to stumps/pad, pad contact status, and time since release
- Once the ball passes the stumps, each sample from that delivery is labelled with whether the ball hit the stumps (`willHitStumps`)
- Data is exported as CSV and JSON from `LBWData.cs`

### Training (Python / PyTorch)
- The model is a fully connected neural network with 5 layers (128 → 64 → 32 → 16 → 1)
- Input: 13 features per sample
- Output: probability that the ball would hit the stumps
- Features are normalised using a `StandardScaler`, saved as `scaler.pkl`
- The best model (by validation loss) is saved as `lbw_model_best.pth`
- Training and validation loss/accuracy are plotted and saved

### Testing (Python)
- `test_lbw_model.py` loads the saved model and scaler, runs predictions on a test CSV, and outputs:
  - Accuracy, precision, recall
  - Confusion matrix
  - Probability distribution plots
  - Threshold sensitivity analysis

### Deployment (Unity / Barracuda)
- The trained model is exported to ONNX format
- Scaler parameters (mean and scale per feature) are exported to JSON
- Unity loads the ONNX model via the Barracuda package (`LBWPredictor.cs`)
- Features are manually normalised using the scaler JSON before inference
- `BallTracker.cs` triggers the prediction just before the ball reaches the pad, and the result is displayed via a colour indicator

---

## Project Structure

```text
2dLBW/
├── Unity/
│   └── 2dLBW/
│       └── Assets/
│           ├── Resources/
│           │   ├── *.onnx              # ONNX model
│           │   └── scaler_params.json  # Normalisation parameters
│           ├── LBWTrainingData.csv
│           ├── LBWTestData.csv
│           └── Scripts/
│               ├── Bowling.cs
│               ├── BallTracker.cs
│               ├── BallSpawner.cs
│               ├── FastDataCollector.cs
│               ├── LBWData.cs
│               ├── LBWPredictor.cs
│               ├── Pad.cs
│               ├── Stumps.cs
│               ├── TestDataLogger.cs
│               └── fpsManager.cs
└── Python/
    ├── train_lbw_model.py
    ├── test_lbw_model.py
    ├── lbw_model_best.pth
    ├── lbw_model_final.pth
    ├── scaler.pkl
    └── Images/
        ├── training_history.png
        ├── feature_importance.png
        ├── test_results.png
        └── threshold_analysis.png
```

---

## Getting Started

### Requirements

**Unity**
- Unity Hub
- Unity 2022+ (recommended)
- Barracuda package (via Package Manager)

**Python**
```text 
torch
pandas
numpy
scikit-learn
matplotlib
seaborn
```

Install with:
```bash 
pip install torch pandas numpy scikit-learn matplotlib seaborn
``` 

---

### Running the Project

#### Play the Game
Download a build from the [Releases](../../releases) page and run it directly — no Unity Hub required.

#### Open in Unity
1. Clone or download the repository
2. Open the `Unity/2dLBW` folder in Unity Hub
3. The ONNX model and scaler JSON are in `Assets/Resources`

#### Swap Models
Older models and their scaler JSONs have been left in the project. To test them:
1. Replace the `.onnx` file in `Assets/Resources`
2. Replace the corresponding `scaler_params.json`
3. Reassign them in the `LBWPredictor` component in the Inspector

#### Collect New Training Data
In Unity, use the **Fast Record** button to collect training data, or **Test Record** to collect test data. Both export CSV and JSON to `Assets/`. Test collection targets fewer samples (15,000 vs 25,000).

#### Train the Model
```bash
cd Python
python train_lbw_model.py
``` 

Expects `../Unity/2dLBW/Assets/LBWTrainingData.csv` to exist.

#### Test the Model
```bash 
cd Python
python test_lbw_model.py
``` 

Expects `../Unity/2dLBW/Assets/LBWTestData.csv` to exist.

---

## Controls

| Input | Action |
|---|---|
| Left Click | Bowl ball (drag to aim) |
| Space | Reset ball |
| L | Capture current ball state as test sample |
| K | Save all captured test samples to file |

UI buttons are available to toggle between **TopSpin** and **BackSpin**, and to trigger data collection.

---

## Original Features (Model Inputs)

| # | Feature | Description |
|---|---|---|
| 0 | `spinType` | 0 = BackSpin, 1 = TopSpin |
| 1 | `speed` | Speed multiplier applied at release |
| 2 | `spinAmount` | Magnitude of torque applied |
| 3 | `timeSinceRelease` | Seconds elapsed since bowling |
| 4 | `ballPosX` | Ball X position |
| 5 | `ballPosY` | Ball Y position |
| 6 | `ballVelX` | Ball X velocity |
| 7 | `ballVelY` | Ball Y velocity |
| 8 | `ballAngularVel` | Ball angular velocity |
| 9 | `distanceToStumps` | Distance from ball to stumps |
| 10 | `distanceToPad` | Distance from ball to pad |
| 11 | `hitPad` | Whether ball has contacted pad (0/1) |
| 12 | `reachedPad` | Whether ball has reached pad X position (0/1) |


## Updated Feautures

| # | Feature | Description |
|---|---|---|
| 0 | `impactPosX` | Ball X position at impact |
| 1 | `impactPosY` | Ball Y position at impact |
| 2 | `impactVelX` | Ball X velocity at impact |
| 3 | `impactVelY` | Ball Y velocity at impact |
| 4 | `impactAngularVel` | Ball Angular velocity at impact |
| 5 | `spinDirection` | Direction of spin |
| 6 | `distanceToStumps` | Distance to stumps at impact|


impactPosX', 'impactPosY', 'impactVelX', 'impactVelY',
                     'impactAngularVel', 'spinDirection', 'distanceToStumps'
---

## Reflections

Model accuracy on paper reached the high 90s, but real in-game performance was notably weaker — particularly for TopSpin deliveries hitting the upper half of the pad. Getting the balance right between random deliveries, mid-range "tricky" deliveries, and tightly focused deliveries proved to be the central challenge of the project.

### What I'd Do Differently (And What I Ended Up Doing Differently)

- **Remove `timeSinceRelease`** — it complicated the decision timing. Without it, predictions could fire directly on pad contact, which is more realistic and simpler to implement.

- **Only record data at pad contact** — real ball-tracking systems capture impact position, velocity, spin, and distance to stumps at the moment of impact. A single snapshot per delivery would be cleaner and more representative than multiple frames sampled in flight.

- **More deliveries, fewer frames** — ~1,500 deliveries across 25,000 samples was a limiting factor. 10,000 deliveries with one sample each would likely generalise better.

- **Variable spin magnitude** — both spin types used a fixed torque of 10 units. Varying this would produce a more diverse and realistic dataset.

---

## Visualisations

Model training charts and test result plots are saved to `Python/Images/`:

- `training_history.png` — loss and accuracy over epochs
- `feature_importance.png` — first-layer weight magnitudes per feature
- `test_results.png` — confusion matrix, probability distributions, confidence scatter
- `threshold_analysis.png` — accuracy, precision, and recall across decision thresholds

---

## Notes

- This is a learning project, not a polished product. The goal was to understand neural networks, PyTorch, backpropagation, and the full pipeline from data collection to in-engine deployment — all within a short development window.
- AI assistance (Claude) was used extensively for code generation, particularly for Unity scripting. All generated code was reviewed, debugged, and modified based on direct understanding of the system.
- See `Thoughts.md` for a full personal reflection on the development process.
```
