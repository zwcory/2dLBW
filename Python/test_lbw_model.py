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

def analyze_by_spin_type(df, predictions_binary, predictions_proba, y_true):
    """Analyze results broken down by spin type"""

    print("\n" + "="*60)
    print("SPIN TYPE ANALYSIS")
    print("="*60)

    spin_types = {0: 'TopSpin', 1: 'BackSpin'}

    for spin_val, spin_name in spin_types.items():
        mask = df['spinType'] == spin_val

        if mask.sum() == 0:
            print(f"\nNo samples for {spin_name}")
            continue

        y_true_spin = y_true[mask]
        pred_spin = predictions_binary[mask]
        prob_spin = predictions_proba[mask]

        accuracy = np.mean(pred_spin == y_true_spin)

        print(f"\n{spin_name} Deliveries:")
        print(f"  Total samples: {mask.sum()}")
        print(f"  Accuracy: {accuracy:.2%}")

        # Confusion matrix for this spin type
        if len(np.unique(y_true_spin)) > 1:
            cm = confusion_matrix(y_true_spin, pred_spin)
            tn, fp, fn, tp = cm.ravel()

            print(f"  True Negatives (Correct MISS):  {tn}")
            print(f"  False Positives (Wrong OUT):    {fp}")
            print(f"  False Negatives (Wrong MISS):   {fn}")
            print(f"  True Positives (Correct OUT):   {tp}")

            if tp + fn > 0:
                recall = tp / (tp + fn)
                print(f"  Recall: {recall:.2%}")

            if tp + fp > 0:
                precision = tp / (tp + fp)
                print(f"  Precision: {precision:.2%}")

        # Average probability for hits vs misses
        hit_mask = y_true_spin == 1
        miss_mask = y_true_spin == 0

        if hit_mask.sum() > 0:
            avg_prob_hits = prob_spin[hit_mask].mean()
            print(f"  Avg probability (actual hits): {avg_prob_hits:.3f}")

        if miss_mask.sum() > 0:
            avg_prob_misses = prob_spin[miss_mask].mean()
            print(f"  Avg probability (actual misses): {avg_prob_misses:.3f}")

def test_from_csv(test_csv_path, threshold=0.55):
    """
    Test model using CSV data from Unity

    Args:
        test_csv_path: Path to test CSV file
        threshold: Decision threshold (default 0.55)
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

    # Analyze by spin type
    analyze_by_spin_type(df, predictions_binary, predictions_proba, y_true)

    # Show some example predictions
    print("\n" + "="*60)
    print("EXAMPLE PREDICTIONS (5 per spin type)")
    print("="*60)

    spin_names = {0: 'TopSpin', 1: 'BackSpin'}

    for spin_val, spin_name in spin_names.items():
        mask = df['spinType'] == spin_val
        indices = np.where(mask)[0][:5]  # First 5 of this spin type

        if len(indices) == 0:
            continue

        print(f"\n{spin_name} Examples:")

        for idx in indices:
            print(f"\n  Sample {idx+1}:")
            print(f"    Speed: {X_test[idx][1]:.2f}x")
            print(f"    Ball Position: ({X_test[idx][4]:.2f}, "
                  f"{X_test[idx][5]:.2f})")
            print(f"    Hit Pad: {'Yes' if X_test[idx][11] == 1 else 'No'}")
            print(f"    Predicted Probability: {predictions_proba[idx]:.2%}")
            print(f"    Prediction: {'HIT' if predictions_binary[idx] == 1 else 'MISS'}")
            print(f"    Actual: {'HIT' if y_true[idx] == 1 else 'MISS'}")
            print(f"    {'✓ CORRECT' if predictions_binary[idx] == y_true[idx] else '✗ WRONG'}")

    # Visualizations
    plot_test_results(df, y_true, predictions_proba, predictions_binary, threshold)


    return accuracy, predictions_proba, predictions_binary

def plot_test_results(
        df, y_true, predictions_proba, predictions_binary, threshold
):
    """Create visualizations of test results including spin type breakdown"""

    fig = plt.figure(figsize=(16, 12))
    gs = fig.add_gridspec(3, 3, hspace=0.3, wspace=0.3)

    spin_names = {0: 'TopSpin', 1: 'BackSpin'}
    spin_colors = {0: 'blue', 1: 'red'}

    # 1. Overall Confusion Matrix
    ax1 = fig.add_subplot(gs[0, 0])
    cm = confusion_matrix(y_true, predictions_binary)
    sns.heatmap(cm, annot=True, fmt='d', cmap='Blues', ax=ax1,
                xticklabels=['Miss', 'Hit'], yticklabels=['Miss', 'Hit'])
    ax1.set_title('Overall Confusion Matrix')
    ax1.set_ylabel('Actual')
    ax1.set_xlabel('Predicted')

    # 2. Confusion Matrix by Spin Type
    for i, (spin_val, spin_name) in enumerate(spin_names.items()):
        ax = fig.add_subplot(gs[0, i+1])

        mask = df['spinType'] == spin_val
        if mask.sum() == 0:
            ax.text(0.5, 0.5, f'No {spin_name}\ndata',
                    ha='center', va='center')
            ax.set_title(f'{spin_name} Confusion Matrix')
            continue

        y_true_spin = y_true[mask]
        pred_spin = predictions_binary[mask]

        if len(np.unique(y_true_spin)) > 1:
            cm_spin = confusion_matrix(y_true_spin, pred_spin)
            sns.heatmap(cm_spin, annot=True, fmt='d',
                        cmap='Reds' if spin_val == 0 else 'Greens',
                        ax=ax,
                        xticklabels=['Miss', 'Hit'],
                        yticklabels=['Miss', 'Hit'])
        else:
            ax.text(0.5, 0.5, 'Insufficient\nvariation',
                    ha='center', va='center')

        ax.set_title(f'{spin_name} Confusion Matrix')
        ax.set_ylabel('Actual')
        ax.set_xlabel('Predicted')

    # 3. Probability Distribution Overall
    ax3 = fig.add_subplot(gs[1, 0])
    ax3.hist(predictions_proba[y_true == 0], bins=20, alpha=0.6,
             label='Actually Missed', color='green')
    ax3.hist(predictions_proba[y_true == 1], bins=20, alpha=0.6,
             label='Actually Hit', color='red')
    ax3.axvline(threshold, color='black', linestyle='--',
                label=f'Threshold ({threshold})')
    ax3.set_xlabel('Predicted Probability')
    ax3.set_ylabel('Count')
    ax3.set_title('Overall Probability Distribution')
    ax3.legend()
    ax3.grid(True, alpha=0.3)

    # 4. Probability Distribution by Spin Type
    for i, (spin_val, spin_name) in enumerate(spin_names.items()):
        ax = fig.add_subplot(gs[1, i+1])

        mask = df['spinType'] == spin_val
        if mask.sum() == 0:
            continue

        prob_spin = predictions_proba[mask]
        y_true_spin = y_true[mask]

        ax.hist(prob_spin[y_true_spin == 0], bins=15, alpha=0.6,
                label='Missed', color='green')
        ax.hist(prob_spin[y_true_spin == 1], bins=15, alpha=0.6,
                label='Hit', color='red')
        ax.axvline(threshold, color='black', linestyle='--')
        ax.set_xlabel('Predicted Probability')
        ax.set_ylabel('Count')
        ax.set_title(f'{spin_name} Probability Distribution')
        ax.legend()
        ax.grid(True, alpha=0.3)

    # 5. Prediction Confidence (colored by spin type)
    ax5 = fig.add_subplot(gs[2, 0])
    for spin_val, spin_name in spin_names.items():
        mask = df['spinType'] == spin_val
        if mask.sum() == 0:
            continue
        indices = np.where(mask)[0]
        ax5.scatter(indices, predictions_proba[mask],
                    c=spin_colors[spin_val], alpha=0.5,
                    label=spin_name, s=20)
    ax5.axhline(threshold, color='black', linestyle='--',
                label='Threshold')
    ax5.set_xlabel('Sample Index')
    ax5.set_ylabel('Predicted Probability')
    ax5.set_title('Prediction Confidence by Spin Type')
    ax5.legend()
    ax5.grid(True, alpha=0.3)

    # 6. Accuracy by Spin Type
    ax6 = fig.add_subplot(gs[2, 1])
    spin_accuracies = []
    spin_labels = []

    for spin_val, spin_name in spin_names.items():
        mask = df['spinType'] == spin_val
        if mask.sum() > 0:
            acc = (predictions_binary[mask] == y_true[mask]).mean()
            spin_accuracies.append(acc)
            spin_labels.append(f'{spin_name}\n(n={mask.sum()})')

    bars = ax6.bar(range(len(spin_labels)), spin_accuracies,
                   color=[spin_colors[i] for i in range(len(spin_labels))],
                   alpha=0.7)
    ax6.set_xticks(range(len(spin_labels)))
    ax6.set_xticklabels(spin_labels)
    ax6.set_ylabel('Accuracy')
    ax6.set_title('Accuracy by Spin Type')
    ax6.set_ylim(0, 1.1)
    ax6.grid(True, alpha=0.3, axis='y')

    # Add accuracy values on bars
    for i, (bar, acc) in enumerate(zip(bars, spin_accuracies)):
        ax6.text(bar.get_x() + bar.get_width()/2, acc + 0.02,
                 f'{acc:.1%}', ha='center', va='bottom', fontweight='bold')

    # 7. Error analysis by spin type
    ax7 = fig.add_subplot(gs[2, 2])
    error_types = {'False Positive': [], 'False Negative': []}

    for spin_val, spin_name in spin_names.items():
        mask = df['spinType'] == spin_val
        if mask.sum() == 0:
            continue

        pred_spin = predictions_binary[mask]
        y_true_spin = y_true[mask]

        fp = ((pred_spin == 1) & (y_true_spin == 0)).sum()
        fn = ((pred_spin == 0) & (y_true_spin == 1)).sum()

        error_types['False Positive'].append(fp)
        error_types['False Negative'].append(fn)

    x = np.arange(len(spin_names))
    width = 0.35

    ax7.bar(x - width/2, error_types['False Positive'], width,
            label='False Positive (Wrong OUT)', color='orange', alpha=0.7)
    ax7.bar(x + width/2, error_types['False Negative'], width,
            label='False Negative (Wrong MISS)', color='purple', alpha=0.7)

    ax7.set_xlabel('Spin Type')
    ax7.set_ylabel('Error Count')
    ax7.set_title('Error Types by Spin')
    ax7.set_xticks(x)
    ax7.set_xticklabels([spin_names[i] for i in range(len(spin_names))])
    ax7.legend()
    ax7.grid(True, alpha=0.3, axis='y')

    plt.savefig('test_results_with_spin.png', dpi=150, bbox_inches='tight')
    print("\nSaved visualization to test_results_with_spin.png")
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