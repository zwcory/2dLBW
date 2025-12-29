import numpy as np
import torch
import pickle
from train_lbw_model import LBWPredictor

def test_single_sample():
    """Test a single sample from Unity"""

    # Load model
    model = LBWPredictor(input_size=13)
    model.load_state_dict(torch.load('lbw_model_best.pth'))
    model.eval()

    # Load scaler
    with open('scaler.pkl', 'rb') as f:
        scaler = pickle.load(f)

    # Close miss
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

    print("="*60)
    print("TESTING SINGLE SAMPLE FROM UNITY")
    print("="*60)

    print("\nRaw features:")
    feature_names = ['spinType', 'speed', 'spinAmount', 'timeSinceRelease',
                     'ballPosX', 'ballPosY', 'ballVelX', 'ballVelY',
                     'ballAngularVel', 'distanceToStumps', 'distanceToPad',
                     'hitPad', 'reachedPad']

    for name, value in zip(feature_names, test_features[0]):
        print(f"  {name:20s}: {value:12.6f}")

    # Normalize
    test_normalized = scaler.transform(test_features)

    print("\nNormalized features:")
    for name, value in zip(feature_names, test_normalized[0]):
        print(f"  {name:20s}: {value:12.6f}")

    # Convert to tensor
    test_tensor = torch.FloatTensor(test_normalized)

    # Predict
    with torch.no_grad():
        output = model(test_tensor)
        probability = output.item()

    print("\n" + "="*60)
    print("PREDICTION RESULT")
    print("="*60)
    print(f"Probability: {probability:.6f} ({probability*100:.2f}%)")
    print(f"Prediction: {'HIT STUMPS' if probability >= 0.5 else 'MISS STUMPS'}")

    if probability >= 0.5:
        print(f"Decision: OUT (hit pad and would hit stumps)")
    else:
        print(f"Decision: NOT OUT (would miss stumps)")

    print("\n" + "="*60)

    return probability

if __name__ == '__main__':
    test_single_sample()