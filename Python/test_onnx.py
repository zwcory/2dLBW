import torch
import onnxruntime as ort
import numpy as np
import pickle

# Load scaler
with open('scaler.pkl', 'rb') as f:
    scaler = pickle.load(f)

# Test data (TopSpin that should MISS)
test_features = np.array([[
    0,          # spinType (TopSpin)
    0.9303377,  # speed
    10,         # spinAmount
    0.7003098,  # timeSinceRelease
    5.89351,    # ballPosX
    -1.778583,  # ballPosY
    17.77925,   # ballVelX
    5.443715,   # ballVelY
    128.9171,   # ballAngularVel
    2.434987,   # distanceToStumps
    1.046815,   # distanceToPad
    1,          # hitPad
    1           # reachedPad
]], dtype=np.float32)

# Normalize
test_normalized = scaler.transform(test_features)

print("Normalized features:")
print(test_normalized)

# Load ONNX model
sess = ort.InferenceSession('lbw_model_legacy.onnx')
input_name = sess.get_inputs()[0].name

# Run inference
result = sess.run(None, {input_name: test_normalized})

print(f"\nModel output: {result[0][0][0]:.6f}")
print(f"Should be < 0.5 for a miss")