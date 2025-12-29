import pickle
import json
import numpy as np

def export_scaler_to_json(scaler_path='scaler.pkl', output_path='scaler_params.json'):
    """Export StandardScaler parameters to JSON for Unity"""

    with open(scaler_path, 'rb') as f:
        scaler = pickle.load(f)

    # Extract mean and scale (std) for each feature
    params = {
        'mean': scaler.mean_.tolist(),
        'scale': scaler.scale_.tolist(),
        'feature_names': [
            'spinType', 'speed', 'spinAmount', 'timeSinceRelease',
            'ballPosX', 'ballPosY', 'ballVelX', 'ballVelY',
            'ballAngularVel', 'distanceToStumps', 'distanceToPad',
            'hitPad', 'reachedPad'
        ]
    }

    with open(output_path, 'w') as f:
        json.dump(params, f, indent=2)

    print(f"✓ Exported scaler parameters to {output_path}")
    print(f"  Mean values: {len(params['mean'])} features")
    print(f"  Scale values: {len(params['scale'])} features")

    return params

if __name__ == '__main__':
    export_scaler_to_json()