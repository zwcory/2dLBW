import pandas as pd
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader, random_split
from sklearn.preprocessing import StandardScaler
import matplotlib.pyplot as plt
import json
import pickle
import os

# Load data
def load_data_from_csv(filepath):
    """Load data from Unity CSV export"""
    df = pd.read_csv(filepath)

    # Strip whitespace from column names
    df.columns = df.columns.str.strip()

    # Features - 13 total
    X = df[['spinType', 'speed', 'spinAmount', 'timeSinceRelease',
            'ballPosX', 'ballPosY', 'ballVelX', 'ballVelY',
            'ballAngularVel', 'distanceToStumps', 'distanceToPad',
            'hitPad', 'reachedPad']].values

    # Labels
    y = df['willHitStumps'].values

    return X, y

def load_data_from_json(filepath):
    """Load data from Unity JSON export"""
    with open(filepath, 'r') as f:
        data = json.load(f)

    examples = data['examples']
    X = []
    y = []

    for ex in examples:
        X.append([
            ex['spinType'], ex['speed'], ex['spinAmount'],
            ex['timeSinceRelease'],
            ex['ballPosX'], ex['ballPosY'], ex['ballVelX'], ex['ballVelY'],
            ex['ballAngularVel'], ex['distanceToStumps'],
            ex['distanceToPad'], ex['hitPad'], ex['reachedPad']
        ])
        y.append(ex['willHitStumps'])

    return np.array(X, dtype=np.float32), np.array(y, dtype=np.float32)


# Dataset class
class LBWDataset(Dataset):
    def __init__(self, X, y):
        self.X = torch.FloatTensor(X)
        self.y = torch.FloatTensor(y).unsqueeze(1)

    def __len__(self):
        return len(self.X)

    def __getitem__(self, idx):
        return self.X[idx], self.y[idx]


# Neural Network Model
class LBWPredictor(nn.Module):
    def __init__(self, input_size=13):
        super(LBWPredictor, self).__init__()

        self.network = nn.Sequential(
            nn.Linear(input_size, 128),
            nn.ReLU(),
            nn.Dropout(0.2),

            nn.Linear(128, 64),
            nn.ReLU(),
            nn.Dropout(0.2),

            nn.Linear(64, 32),
            nn.ReLU(),

            nn.Linear(32, 16),
            nn.ReLU(),

            nn.Linear(16, 1),
            nn.Sigmoid()
        )

    def forward(self, x):
        return self.network(x)


# Training function
def train_model(model, train_loader, val_loader, epochs=100, lr=0.001):
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    model = model.to(device)

    criterion = nn.BCELoss()
    optimizer = optim.Adam(model.parameters(), lr=lr)

    train_losses = []
    val_losses = []
    train_accuracies = []
    val_accuracies = []

    best_val_loss = float('inf')

    for epoch in range(epochs):
        # Training
        model.train()
        train_loss = 0
        train_correct = 0
        train_total = 0

        for inputs, labels in train_loader:
            inputs, labels = inputs.to(device), labels.to(device)

            optimizer.zero_grad()
            outputs = model(inputs)
            loss = criterion(outputs, labels)
            loss.backward()
            optimizer.step()

            train_loss += loss.item()
            predictions = (outputs > 0.5).float()
            train_correct += (predictions == labels).sum().item()
            train_total += labels.size(0)

        # Validation
        model.eval()
        val_loss = 0
        val_correct = 0
        val_total = 0

        with torch.no_grad():
            for inputs, labels in val_loader:
                inputs, labels = inputs.to(device), labels.to(device)
                outputs = model(inputs)
                loss = criterion(outputs, labels)

                val_loss += loss.item()
                predictions = (outputs > 0.5).float()
                val_correct += (predictions == labels).sum().item()
                val_total += labels.size(0)

        # Calculate metrics
        train_loss /= len(train_loader)
        val_loss /= len(val_loader)
        train_acc = 100 * train_correct / train_total
        val_acc = 100 * val_correct / val_total

        train_losses.append(train_loss)
        val_losses.append(val_loss)
        train_accuracies.append(train_acc)
        val_accuracies.append(val_acc)

        # Save best model
        if val_loss < best_val_loss:
            best_val_loss = val_loss
            torch.save(model.state_dict(), 'lbw_model_best.pth')

        if (epoch + 1) % 10 == 0:
            print(f'Epoch [{epoch+1}/{epochs}]')
            print(f'  Train Loss: {train_loss:.4f}, Train Acc: {train_acc:.2f}%')
            print(f'  Val Loss: {val_loss:.4f}, Val Acc: {val_acc:.2f}%')

    return train_losses, val_losses, train_accuracies, val_accuracies


# Plot training history
def plot_training_history(train_losses, val_losses, train_accs, val_accs):
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 4))

    # Loss plot
    ax1.plot(train_losses, label='Train Loss')
    ax1.plot(val_losses, label='Val Loss')
    ax1.set_xlabel('Epoch')
    ax1.set_ylabel('Loss')
    ax1.set_title('Training and Validation Loss')
    ax1.legend()
    ax1.grid(True)

    # Accuracy plot
    ax2.plot(train_accs, label='Train Acc')
    ax2.plot(val_accs, label='Val Acc')
    ax2.set_xlabel('Epoch')
    ax2.set_ylabel('Accuracy (%)')
    ax2.set_title('Training and Validation Accuracy')
    ax2.legend()
    ax2.grid(True)

    plt.tight_layout()
    plt.savefig('training_history.png')
    plt.show()


# Plot feature importance
def plot_feature_importance(model, feature_names):
    """Visualize which features the model considers most important"""

    # Get weights from first layer
    first_layer_weights = model.network[0].weight.data.cpu().numpy()

    # Calculate average absolute weight per feature
    importance = np.abs(first_layer_weights).mean(axis=0)

    # Sort features by importance
    indices = np.argsort(importance)[::-1]
    sorted_features = [feature_names[i] for i in indices]
    sorted_importance = importance[indices]

    # Plot
    plt.figure(figsize=(10, 6))
    plt.barh(range(len(sorted_features)), sorted_importance)
    plt.yticks(range(len(sorted_features)), sorted_features)
    plt.xlabel('Average Absolute Weight')
    plt.title('Feature Importance (First Layer Weights)')
    plt.tight_layout()
    plt.savefig('feature_importance.png')
    plt.show()


# Main training pipeline
def main():
    print("Loading data...")

    # Path to CSV file (relative to this script in 2dLBW/Python/)
    csv_path = '../Unity/2dLBW/Assets/LBWTrainingData.csv'

    # Check if file exists
    if not os.path.exists(csv_path):
        print(f"ERROR: Could not find CSV file at {csv_path}")
        print("Please check the file path!")
        return

    X, y = load_data_from_csv(csv_path)

    print(f"Dataset size: {len(X)}")
    print(f"Features: {X.shape[1]}")
    print(f"Positive samples (hit stumps): {np.sum(y == 1)} ({100*np.mean(y):.1f}%)")
    print(f"Negative samples (miss stumps): {np.sum(y == 0)} ({100*(1-np.mean(y)):.1f}%)")

    # Check for severe class imbalance
    hit_rate = np.mean(y)
    if hit_rate < 0.1 or hit_rate > 0.9:
        print(f"\n⚠️  WARNING: Severe class imbalance detected!")
        print(f"Consider collecting more balanced data for better training.")

    # Normalize features
    scaler = StandardScaler()
    X = scaler.fit_transform(X)

    # Save scaler for later use
    with open('scaler.pkl', 'wb') as f:
        pickle.dump(scaler, f)
    print("Saved scaler to scaler.pkl")

    # Create dataset
    dataset = LBWDataset(X, y)

    # Split into train and validation
    train_size = int(0.8 * len(dataset))
    val_size = len(dataset) - train_size
    train_dataset, val_dataset = random_split(dataset, [train_size, val_size])

    print(f"\nTrain samples: {train_size}")
    print(f"Validation samples: {val_size}")

    # Create data loaders
    train_loader = DataLoader(train_dataset, batch_size=32, shuffle=True)
    val_loader = DataLoader(val_dataset, batch_size=32, shuffle=False)

    # Create model
    model = LBWPredictor(input_size=13)
    print(f"\nModel architecture:\n{model}\n")

    # Count parameters
    total_params = sum(p.numel() for p in model.parameters())
    print(f"Total parameters: {total_params:,}\n")

    # Train
    print("Starting training...")
    train_losses, val_losses, train_accs, val_accs = train_model(
        model, train_loader, val_loader, epochs=100, lr=0.001
    )

    # Save final model
    torch.save(model.state_dict(), 'lbw_model_final.pth')
    print("\nTraining complete!")
    print("Models saved:")
    print("  - lbw_model_best.pth (best validation loss)")
    print("  - lbw_model_final.pth (final epoch)")
    print("  - scaler.pkl (feature normalization)")

    # Plot results
    print("\nGenerating visualizations...")
    plot_training_history(train_losses, val_losses, train_accs, val_accs)

    # Plot feature importance
    feature_names = ['spinType', 'speed', 'spinAmount', 'timeSinceRelease',
                     'ballPosX', 'ballPosY', 'ballVelX', 'ballVelY',
                     'ballAngularVel', 'distanceToStumps', 'distanceToPad',
                     'hitPad', 'reachedPad']
    plot_feature_importance(model, feature_names)

    print("Saved visualizations:")
    print("  - training_history.png")
    print("  - feature_importance.png")


if __name__ == '__main__':
    main()