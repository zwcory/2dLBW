import pandas as pd
import numpy as np
import torch
import pickle
from train_lbw_model import LBWPredictor
from sklearn.metrics import confusion_matrix, classification_report
import matplotlib.pyplot as plt
import seaborn as sns

def load_model_and_scaler():
    """Load the trained model and scaler"""
    model = LBWPredictor(input_size=13)
    model.load_state_dict(torch.load('lbw_model_best.pth'))
    model.eval()

    with open('scaler.pkl', 'rb') as f:
        scaler = pickle.load(f)

    return model, scaler

def predict_lbw(model, scaler, features):
    """
    Predict if ball will hit stumps

    Features order:
    [spinType, speed, spinAmount, timeSinceRelease,
     ballPosX, ballPosY, ballVelX, ballVelY,
     ballAngularVel, distanceToStumps, distanceToPad,
     hitPad, reachedPad]
    """
    # Normalize features
    features_scaled = scaler.transform([features])

    # Convert to tensor
    features_tensor = torch.FloatTensor(features_scaled)

    # Predict
    with torch.no_grad():
        output = model(features_tensor)
        probability = output.item()

    return probability

def test_from_csv(test_csv_path, threshold=0.5):
    """
    Test model using CSV data from Unity

    Args:
        test_csv_path: Path to test CSV file
        threshold: Decision threshold (default 0.5)
    """
    print("="*60)
    print("TESTING LBW MODEL WITH UNITY DATA")
    print("="*60)

    # Load model and scaler
    model, scaler = load_model_and_scaler()

    # Load test data
    df = pd.read_csv(test_csv_path)
    df.columns = df.columns.str.strip()

    print(f"\nLoaded {len(df)} test samples from {test_csv_path}")
    print(f"Test set distribution:")
    print(f"  Hit stumps: {df['willHitStumps'].sum()} ({100*df['willHitStumps'].mean():.1f}%)")
    print(f"  Missed stumps: {len(df) - df['willHitStumps'].sum()} ({100*(1-df['willHitStumps'].mean()):.1f}%)")

    # Extract features
    X_test = df[['spinType', 'speed', 'spinAmount', 'timeSinceRelease',
                 'ballPosX', 'ballPosY', 'ballVelX', 'ballVelY',
                 'ballAngularVel', 'distanceToStumps', 'distanceToPad',
                 'hitPad', 'reachedPad']].values

    y_true = df['willHitStumps'].values

    # Make predictions
    print("\nMaking predictions...")
    predictions_proba = []
    predictions_binary = []

    for features in X_test:
        prob = predict_lbw(model, scaler, features)
        predictions_proba.append(prob)
        predictions_binary.append(1 if prob >= threshold else 0)

    predictions_proba = np.array(predictions_proba)
    predictions_binary = np.array(predictions_binary)

    # Calculate metrics
    accuracy = np.mean(predictions_binary == y_true)

    print("\n" + "="*60)
    print("RESULTS")
    print("="*60)
    print(f"\nAccuracy: {accuracy:.2%}")
    print(f"Threshold: {threshold}")

    # Confusion Matrix
    cm = confusion_matrix(y_true, predictions_binary)
    tn, fp, fn, tp = cm.ravel()

    print("\nConfusion Matrix:")
    print(f"  True Negatives (Correct MISS):  {tn}")
    print(f"  False Positives (Wrong OUT):    {fp}")
    print(f"  False Negatives (Wrong MISS):   {fn}")
    print(f"  True Positives (Correct OUT):   {tp}")

    # Additional metrics
    if tp + fn > 0:
        recall = tp / (tp + fn)
        print(f"\nRecall (catches actual hits): {recall:.2%}")

    if tp + fp > 0:
        precision = tp / (tp + fp)
        print(f"Precision (correct when predicting hit): {precision:.2%}")

    # Classification report
    print("\nDetailed Classification Report:")
    print(classification_report(y_true, predictions_binary,
                                target_names=['Miss Stumps', 'Hit Stumps']))

    # Show some example predictions
    print("\n" + "="*60)
    print("EXAMPLE PREDICTIONS")
    print("="*60)

    # Show first 10 predictions
    for i in range(min(10, len(df))):
        print(f"\nSample {i+1}:")
        print(f"  Spin: {'TopSpin' if X_test[i][0] == 1 else 'BackSpin'}")
        print(f"  Speed: {X_test[i][1]:.2f}x")
        print(f"  Ball Position: ({X_test[i][4]:.2f}, {X_test[i][5]:.2f})")
        print(f"  Hit Pad: {'Yes' if X_test[i][11] == 1 else 'No'}")
        print(f"  Reached Pad: {'Yes' if X_test[i][12] == 1 else 'No'}")
        print(f"  Predicted Probability: {predictions_proba[i]:.2%}")
        print(f"  Prediction: {'HIT STUMPS' if predictions_binary[i] == 1 else 'MISS STUMPS'}")
        print(f"  Actual: {'HIT STUMPS' if y_true[i] == 1 else 'MISS STUMPS'}")
        print(f"  {'✓ CORRECT' if predictions_binary[i] == y_true[i] else '✗ WRONG'}")

    # Visualizations
    plot_test_results(y_true, predictions_proba, predictions_binary, threshold)

    return accuracy, predictions_proba, predictions_binary

def plot_test_results(y_true, predictions_proba, predictions_binary, threshold):
    """Create visualizations of test results"""

    fig, axes = plt.subplots(2, 2, figsize=(14, 10))

    # 1. Confusion Matrix
    cm = confusion_matrix(y_true, predictions_binary)
    sns.heatmap(cm, annot=True, fmt='d', cmap='Blues', ax=axes[0, 0],
                xticklabels=['Miss', 'Hit'], yticklabels=['Miss', 'Hit'])
    axes[0, 0].set_title('Confusion Matrix')
    axes[0, 0].set_ylabel('Actual')
    axes[0, 0].set_xlabel('Predicted')

    # 2. Probability Distribution
    axes[0, 1].hist(predictions_proba[y_true == 0], bins=20, alpha=0.6,
                    label='Actually Missed', color='green')
    axes[0, 1].hist(predictions_proba[y_true == 1], bins=20, alpha=0.6,
                    label='Actually Hit', color='red')
    axes[0, 1].axvline(threshold, color='black', linestyle='--',
                       label=f'Threshold ({threshold})')
    axes[0, 1].set_xlabel('Predicted Probability')
    axes[0, 1].set_ylabel('Count')
    axes[0, 1].set_title('Prediction Probability Distribution')
    axes[0, 1].legend()
    axes[0, 1].grid(True, alpha=0.3)

    # 3. Prediction Confidence
    correct = predictions_binary == y_true
    axes[1, 0].scatter(range(len(predictions_proba)), predictions_proba,
                       c=correct, cmap='RdYlGn', alpha=0.6)
    axes[1, 0].axhline(threshold, color='black', linestyle='--',
                       label=f'Threshold')
    axes[1, 0].set_xlabel('Sample Index')
    axes[1, 0].set_ylabel('Predicted Probability')
    axes[1, 0].set_title('Prediction Confidence (Green=Correct, Red=Wrong)')
    axes[1, 0].legend()
    axes[1, 0].grid(True, alpha=0.3)

    # 4. Accuracy by Confidence Level
    confidence_bins = np.linspace(0, 1, 11)
    accuracy_by_confidence = []
    counts_by_confidence = []

    for i in range(len(confidence_bins) - 1):
        low, high = confidence_bins[i], confidence_bins[i+1]
        mask = (predictions_proba >= low) & (predictions_proba < high)
        if mask.sum() > 0:
            acc = (predictions_binary[mask] == y_true[mask]).mean()
            accuracy_by_confidence.append(acc)
            counts_by_confidence.append(mask.sum())
        else:
            accuracy_by_confidence.append(0)
            counts_by_confidence.append(0)

    bin_centers = (confidence_bins[:-1] + confidence_bins[1:]) / 2
    axes[1, 1].bar(bin_centers, accuracy_by_confidence, width=0.08,
                   alpha=0.7, label='Accuracy')
    axes[1, 1].set_xlabel('Prediction Probability Range')
    axes[1, 1].set_ylabel('Accuracy')
    axes[1, 1].set_title('Accuracy by Confidence Level')
    axes[1, 1].set_ylim(0, 1.1)
    axes[1, 1].grid(True, alpha=0.3)

    # Add sample counts as text
    for i, (x, y, count) in enumerate(zip(bin_centers, accuracy_by_confidence,
                                          counts_by_confidence)):
        if count > 0:
            axes[1, 1].text(x, y + 0.05, f'n={count}',
                            ha='center', fontsize=8)

    plt.tight_layout()
    plt.savefig('test_results.png', dpi=150)
    print("\nSaved visualization to test_results.png")
    plt.show()

def test_threshold_sensitivity(test_csv_path):
    """Test how different thresholds affect accuracy"""
    print("\n" + "="*60)
    print("THRESHOLD SENSITIVITY ANALYSIS")
    print("="*60)

    model, scaler = load_model_and_scaler()

    # Load test data
    df = pd.read_csv(test_csv_path)
    df.columns = df.columns.str.strip()

    X_test = df[['spinType', 'speed', 'spinAmount', 'timeSinceRelease',
                 'ballPosX', 'ballPosY', 'ballVelX', 'ballVelY',
                 'ballAngularVel', 'distanceToStumps', 'distanceToPad',
                 'hitPad', 'reachedPad']].values

    y_true = df['willHitStumps'].values

    # Get predictions
    predictions_proba = np.array([predict_lbw(model, scaler, x) for x in X_test])

    # Test different thresholds
    thresholds = np.arange(0.1, 1.0, 0.05)
    accuracies = []
    recalls = []
    precisions = []

    for threshold in thresholds:
        pred = (predictions_proba >= threshold).astype(int)
        acc = (pred == y_true).mean()

        tp = ((pred == 1) & (y_true == 1)).sum()
        fp = ((pred == 1) & (y_true == 0)).sum()
        fn = ((pred == 0) & (y_true == 1)).sum()

        recall = tp / (tp + fn) if (tp + fn) > 0 else 0
        precision = tp / (tp + fp) if (tp + fp) > 0 else 0

        accuracies.append(acc)
        recalls.append(recall)
        precisions.append(precision)

    # Find best threshold
    best_idx = np.argmax(accuracies)
    best_threshold = thresholds[best_idx]
    best_accuracy = accuracies[best_idx]

    print(f"\nBest threshold: {best_threshold:.2f}")
    print(f"Best accuracy: {best_accuracy:.2%}")

    # Plot
    plt.figure(figsize=(10, 6))
    plt.plot(thresholds, accuracies, 'b-', label='Accuracy', linewidth=2)
    plt.plot(thresholds, recalls, 'g--', label='Recall', linewidth=2)
    plt.plot(thresholds, precisions, 'r--', label='Precision', linewidth=2)
    plt.axvline(best_threshold, color='black', linestyle=':',
                label=f'Best Threshold ({best_threshold:.2f})')
    plt.xlabel('Decision Threshold')
    plt.ylabel('Score')
    plt.title('Model Performance vs Decision Threshold')
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.tight_layout()
    plt.savefig('threshold_analysis.png', dpi=150)
    print("Saved threshold analysis to threshold_analysis.png")
    plt.show()

    return best_threshold

def main():
    """Main testing function"""
    import os

    # Path to test CSV (relative to Python folder)
    test_csv = '../Unity/2dLBW/Assets/LBWTestData.csv'

    # Check if file exists
    if not os.path.exists(test_csv):
        print(f"ERROR: Test CSV not found at {test_csv}")
        print("Please generate test data in Unity and save as LBWTestData.csv")
        return

    # Run tests
    print("Testing model with Unity test data...\n")

    # Test with default threshold
    accuracy, probs, preds = test_from_csv(test_csv, threshold=0.5)

    # Analyze threshold sensitivity
    best_threshold = test_threshold_sensitivity(test_csv)

    # Re-test with best threshold
    if best_threshold != 0.5:
        print("\n" + "="*60)
        print(f"RE-TESTING WITH OPTIMAL THRESHOLD ({best_threshold:.2f})")
        print("="*60)
        test_from_csv(test_csv, threshold=best_threshold)

if __name__ == '__main__':
    main()