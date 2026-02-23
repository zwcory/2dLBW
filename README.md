# 2D LBW Predictor

A 2D cricket simulation built in **Unity** that uses a **PyTorch neural network** to predict whether a bowled delivery would go on to hit the stumps — recreating the core concept behind real-world LBW (Leg Before Wicket) decision review technology.

Developed as **Week 4** of a 52-projects-in-a-year challenge.

![Unity](https://img.shields.io/badge/Unity-2D-blue)
![PyTorch](https://img.shields.io/badge/PyTorch-Neural%20Network-red)
![ONNX](https://img.shields.io/badge/ONNX-Inference-green)

---

## Table of Contents

- [Overview](#overview)
- [How It Works](#how-it-works)
- [Project Structure](#project-structure)
- [Model Architecture](#model-architecture)
- [Data Collection](#data-collection)
- [Results](#results)
  - [V1 vs V2 Comparison](#v1-vs-v2-comparison)
- [Running the Project](#running-the-project)
- [Swapping Models](#swapping-models)
- [Thoughts and Retrospective](#thoughts-and-retrospective)

---

## Overview

In cricket, the **LBW rule** dismisses a batter if a delivery that strikes their pad would have gone on to hit the stumps. Modern cricket uses **Hawk-Eye** ball-tracking technology to predict ball trajectory after pad impact.

This project simulates that concept in 2D:

1. A ball is bowled towards a pad and stumps in a Unity scene.
2. At the moment the ball strikes the pad, impact data is captured.
3. A trained neural network predicts whether the ball would have continued to hit the stumps.
4. The decision is displayed visually (red = OUT, green = NOT OUT).

---

## How It Works

**At pad impact, the following features are captured:**

| Feature            | Description                              |
|--------------------|------------------------------------------|
| impactPosX         | Ball X position at pad contact           |
| impactPosY         | Ball Y position at pad contact           |
| impactVelX         | Ball X velocity at impact                |
| impactVelY         | Ball Y velocity at impact                |
| impactAngularVel   | Ball angular velocity at impact          |
| spinDirection      | 0 = TopSpin, 1 = BackSpin               |
| distanceToStumps   | Distance from impact point to stumps     |

These features are normalized using a **StandardScaler** (saved alongside the model) and fed into the neural network, which outputs a probability of hitting the stumps.

---

## Project Structure

```
2dLBW/
├── Python/
│   ├── train_model.py              # Training pipeline (PyTorch)
│   ├── test_model.py               # Evaluation and visualisation
│   ├── export_onnx.py              # PyTorch to ONNX conversion
│   ├── Images/                     # Model visualisations
│   │   ├── training_history.png
│   │   ├── confusion_matrix.png
│   │   ├── feature_importance.png
│   │   └── ...
│   ├── lbw_model_best.pth          # Best model checkpoint
│   ├── scaler.pkl                  # Fitted StandardScaler
│   └── scaler_params.json          # Scaler exported for Unity
│
├── Unity/2dLBW/
│   ├── Assets/
│   │   ├── Resources/
│   │   │   ├── lbw_model.onnx              # ONNX model for inference
│   │   │   └── scaler_params.json          # Scaler params for Unity
│   │   ├── Scripts/
│   │   │   ├── Bowling.cs                  # Ball bowling mechanics
│   │   │   ├── Pad.cs                      # Pad impact detection and LBW trigger
│   │   │   ├── Stumps.cs                   # Stump hit detection
│   │   │   ├── LBWPredictor.cs             # ONNX inference via Barracuda
│   │   │   ├── BallTracker.cs              # Ball tracking utilities
│   │   │   ├── FastDataCollector.cs        # V1 data collection (frame-based)
│   │   │   ├── ImprovedDataCollector.cs    # V2 data collection (impact-based)
│   │   │   ├── LBWData.cs                 # V1 data storage and export
│   │   │   ├── LBWDataV2.cs               # V2 data storage and export
│   │   │   └── fpsManager.cs              # Locks to 60 FPS
│   │   └── LBWTrainingData_V2.csv         # Training dataset
│   └── ...
│
├── README.md
└── Thoughts.md
```

---

## Model Architecture

A fully connected neural network with binary classification output:

```
Input (7 features)
       |
Linear(7, 128) -> ReLU -> Dropout(0.2)
       |
Linear(128, 64) -> ReLU -> Dropout(0.2)
       |
Linear(64, 32) -> ReLU
       |
Linear(32, 16) -> ReLU
       |
Linear(16, 1) -> Sigmoid
       |
Output: P(will hit stumps)
```

- **Loss:** Binary Cross-Entropy (BCELoss)
- **Optimiser:** Adam (lr=0.001)
- **Epochs:** 100
- **Batch Size:** 32
- **Train/Val Split:** 80/20

The same architecture is used for both V1 and V2 — the key difference is in the data, not the model.

---

## Data Collection

### V1 — Frame-Based Sampling (FastDataCollector)

Recorded **multiple data points per delivery** at a fixed sample rate (0.02s intervals). Each frame captured the ball's current state, producing many samples per single delivery. The final label (hit stumps or not) was applied retroactively to all frames from that delivery.

**Features (13):** spinType, speed, spinAmount, timeSinceRelease, ballPosX, ballPosY, ballVelX, ballVelY, ballAngularVel, distanceToStumps, distanceToPad, hitPad, reachedPad

**Dataset:** ~25,000 frames from ~1,500 deliveries

**Problems:**
- The `timeSinceRelease` feature made it difficult to decide when to call the predictor in-game.
- Many redundant samples per delivery added noise without adding real information.
- Features like `hitPad` and `reachedPad` leaked information about ball progress rather than capturing a clean snapshot.

### V2 — Impact-Based Sampling (ImprovedDataCollector)

Records **one data point per delivery** at the exact moment the ball contacts the pad. The pad's `OnTriggerEnter2D` / `OnCollisionEnter2D` calls `OnPadImpact()` on the data collector, capturing position, velocity, and angular velocity at the precise moment of contact.

**Features (7):** impactPosX, impactPosY, impactVelX, impactVelY, impactAngularVel, spinDirection, distanceToStumps

**Dataset:** ~10,000 deliveries (one record each)

**Improvements:**
- Cleaner data — one meaningful snapshot per delivery rather than many noisy frames.
- Mirrors real-world ball-tracking systems which capture data at a specific point.
- Removed leaked/redundant features, letting the model focus on physics at impact.
- Decision can be triggered naturally on pad contact in-game.

### Data Balance Strategy

Data collection progressively narrows the bowling angle range to generate more "tricky" deliveries (borderline hit/miss), preventing the model from only learning obvious cases:

| Progress | Angle Range        |
|----------|--------------------|
| 0-50%    | -22.5 to -5       |
| 50-75%   | -22.5 to -15      |
| 75-90%   | -21.5 to -17      |
| 90-100%  | -20.5 to -18.5    |

---

## Results

### V1 Model (Frame-Based)

| Metric              | Value     |
|---------------------|-----------|
| **Overall Accuracy**| **99.1%** |
| TopSpin Accuracy    | 99.3%     |
| BackSpin Accuracy   | 98.8%     |
| Best Threshold      | 0.55      |
| False Positives     | 28        |
| False Negatives     | 110       |
| Test Samples        | 15,048    |

**Feature Importance (V1):**

The most influential features were ballVelX, speed, and ballPosY — general trajectory features spread across the flight rather than focused at one point.

### V2 Model (Impact-Based)

| Metric              | Value     |
|---------------------|-----------|
| **Overall Accuracy**| **99.2%** |
| TopSpin Accuracy    | 99.3%     |
| BackSpin Accuracy   | 99.2%     |
| Best Threshold      | 0.35      |
| False Positives     | 15        |
| False Negatives     | 9         |
| Test Samples        | 3,000     |

**Feature Importance (V2):**

impactPosY dominates by a large margin, followed by impactVelY. This aligns with real cricket where ball height is the primary factor in LBW decisions.

### V1 vs V2 Comparison

| Aspect                    | V1 (Frame-Based)              | V2 (Impact-Based)            |
|---------------------------|-------------------------------|------------------------------|
| **Data approach**         | Multiple frames per delivery  | One snapshot at pad impact   |
| **Features**              | 13 (inc. time, distance)      | 7 (physics at impact only)   |
| **Training samples**      | ~25,000 frames / ~1,500 balls | ~10,000 deliveries           |
| **Overall accuracy**      | 99.1%                         | 99.2%                        |
| **False positives**       | 28                            | 15                           |
| **False negatives**       | 110                           | 9                            |
| **Total errors**          | 138                           | 24                           |
| **Best threshold**        | 0.55                          | 0.35                         |
| **Top feature**           | ballVelX                      | impactPosY                   |
| **Probability separation**| Good but some overlap         | Very clean bimodal split     |
| **In-game integration**   | Awkward (when to call?)       | Natural (call on pad hit)    |
| **Training convergence**  | ~20 epochs                    | ~10 epochs                   |

**Key takeaways from the comparison:**

- **Fewer but better data points wins.** V2 uses fewer total samples but each one is more meaningful, resulting in 83% fewer total errors (138 down to 24).
- **False negatives dropped dramatically** from 110 to 9. V1 was far more likely to incorrectly predict a miss when the ball would actually hit — the more dangerous error in an LBW context.
- **Cleaner probability distributions.** V2's predicted probabilities cluster tightly near 0 or 1 with very few uncertain predictions in the middle. V1 had more spread in the 0.2-0.6 range.
- **Feature importance makes more physical sense in V2.** Ball height at impact (impactPosY) being the dominant feature mirrors real-world LBW analysis. V1's reliance on ballVelX suggests it was partially learning trajectory patterns rather than impact physics.
- **Lower optimal threshold in V2 (0.35 vs 0.55)** indicates the model is more confident when predicting hits, allowing a lower bar without sacrificing precision.
- **Faster convergence.** V2 reaches stable accuracy around epoch 10 vs epoch 20 for V1, likely due to cleaner, less noisy training data.

Training curves and confusion matrices for both versions can be found in `/Python`.

---

## Running the Project

### Builds

Pre-built releases are available on the [Releases](../../releases) page.

### From Source

**Requirements:**

- Unity Hub + Unity (version matching the project)
- Python 3.8+ with PyTorch (for retraining only)

**Steps:**

1. Clone the repository.
2. Open `Unity/2dLBW` in Unity Hub.
3. The ONNX model and scaler are already in `Assets/Resources/`.
4. Press Play to bowl deliveries and see LBW predictions.

### Controls

- **Mouse** — Aim the delivery
- **Left Click** — Bowl
- **Space** — Reset ball
- **UI Buttons** — Toggle TopSpin/BackSpin, trigger data collection

### Retraining the Model

```bash
cd Python
pip install torch numpy pandas scikit-learn matplotlib
python train_model.py
python test_model.py
python export_onnx.py
```

Then copy the generated `.onnx` model and `scaler_params.json` into `Unity/2dLBW/Assets/Resources/`.

---

## Swapping Models

Older models and their scalers are included in the project. To test a different model:

1. Navigate to `Assets/Resources/`.
2. Replace the `.onnx` model file with the desired version.
3. Replace `scaler_params.json` with the matching scaler.
4. Enter Play mode — the `LBWPredictor` will load the new model automatically.

**Important:** Always use the scaler that was saved during that model's training. Mismatched scalers will produce incorrect predictions.

---

## Thoughts and Retrospective

Read the full development retrospective in [Thoughts.md](Thoughts.md).

**Key takeaways:**

- Recording data at the **moment of pad impact** (V2) was far cleaner than frame-based sampling (V1).
- **impactPosY** (ball height) dominates predictions — this aligns with real cricket where height is the primary LBW factor.
- Balancing random vs focused deliveries in training data had the biggest impact on real-world accuracy.
- Despite 99%+ test accuracy for v1 and v2, in-game performance on edge cases in v1 (especially topspin hitting the upper pad) remained imperfect while v2 would beat the human eye, a reminder that test metrics don't always tell the full story.

**If starting over, I would:**

- Remove `timeSinceRelease` as a feature entirely (already done in V2).
- Record only at pad impact (already done in V2).
- Increase to 10,000+ deliveries.
- Experiment with varying spin magnitudes rather than a fixed 10 units between the two types.

---

## Disclaimer

This is not a polished product — it is a learning project built in a few days to explore neural networks, PyTorch, ONNX export, and Unity inference. It was developed as part of a **52-projects-in-a-year challenge** where the goal is rapid learning over perfection.

---

## Acknowledgements

- [3Blue1Brown](https://www.youtube.com/c/3blue1brown) — Neural network intuition
- [Rob Mulla](https://www.youtube.com/@robmulla) — PyTorch tutorials
- [Unity Barracuda](https://docs.unity3d.com/Packages/com.unity.barracuda@latest) — ONNX inference in Unity
