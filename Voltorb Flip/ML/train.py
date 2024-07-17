import torch
import torch.nn as nn
import torch.nn.functional as F
import pandas as pd

# hyperparameters
learning_rate = 5e-3
epochs = 10000
eval_interval = 1000
eval_iters = 200
batch_size = 64
device = 'cuda' if torch.cuda.is_available() else 'cpu'
# Training data columns: 0-level, 1:20-voltorb numbers, 21:45-known board state
# Add 1 to input for board index to predict
# What we want to predict: 46:70-full board state
input_sizes = [1, 20, 25, 1]
voltorb_size = 25
conv_size = 1000
hidden_size = 3000
output_size = 4

# Read training data
df = pd.read_csv('training_data.csv', sep='\t')

def normalize(data : pd.DataFrame, *columns : str):
    # Find absolute max and min
    maximum = max([data[columns[i]].max() for i in range(len(columns))])
    minimum = min([data[columns[i]].min() for i in range(len(columns))])

    # MinMax normalize all columns
    for i in range(len(columns)):
        data[columns[i]] = (data[columns[i]] - minimum) / (maximum - minimum)

# MinMax normalization on each column of data separately
normalize(df, 'Level')
# Don't normalize voltorbs for embedding
# normalize(df, *df.columns[1:21])
normalize(df, *df.columns[21:46])
# Don't normalize predictions
# normalize(df, *df.columns[46:71])

# Convert data to tensors
full_data = torch.tensor(df.to_numpy(), dtype=torch.float32, device=device)

# Split data into train and test
n = int(0.9 * len(full_data))
training_data = full_data[:n]
test_data = full_data[n:]

# Model
class Model(nn.Module):
    def __init__(self):
        super().__init__()

        # 15 is highest voltorb number ever recorded in human history (probably)
        self.voltorb_embedding = nn.Embedding(25, voltorb_size)
        self.voltorb_position_embedding = nn.Embedding(input_sizes[1], voltorb_size)
        self.voltorb_layers = nn.Sequential(
            nn.Linear(voltorb_size, conv_size),
        )
        self.known_layers = nn.Sequential(
            nn.Linear(input_sizes[2], conv_size),
            # nn.Conv2d(input_sizes[2], conv_size, (1, 1)),
            # nn.ReLU(),
            # nn.MaxPool2d((1, 1), stride=(1, 1))
        )

        self.hidden_layer = nn.Linear(input_sizes[1], hidden_size)
        self.output_layer = nn.Linear(hidden_size + input_sizes[0] + input_sizes[3], output_size)
        
        self.softmax = nn.Softmax(dim=-1)
        self.flatten = nn.Flatten()
    
    def forward(self, x, y):
        level = x[:, 0:1]
        voltorbs = x[:, 1:21]
        known = x[:, 21:46]#.view(-1, 25, 1, 1)
        index = x[:, 46:47]
        # Voltorb nums
        voltorb_embd = self.voltorb_embedding(voltorbs.long())
        position_embd = self.voltorb_position_embedding(torch.arange(input_sizes[1], device=device))
        voltorbs = self.voltorb_layers(voltorb_embd + position_embd)
        # Known board state
        known = self.known_layers(known).view(-1, conv_size, 1)
        # known = self.flatten(known).view(-1, conv_size, 1)
        # Voltorbs interacts with board
        known = torch.einsum('ijk,ikl->ijl', voltorbs, known).view(-1, input_sizes[1])
        # Hidden layer
        x = self.hidden_layer(known)
        # Output layer
        x = self.output_layer(torch.cat((x, index, level), dim=-1))
        
        # Find loss
        loss = F.cross_entropy(x, y.long().view(-1))
        # Convert to probabilities
        x = self.softmax(x)

        return x, loss

model = Model()
m = model.to(device)
# Optimizer
optimizer = torch.optim.SGD(m.parameters(), lr=learning_rate)

# Get random batch from data
def get_batch(split):
    data = training_data if split == 'train' else test_data
    random_nums = torch.randint(len(data), (batch_size,)) 
    batch = data[random_nums] # (batch_size, 71)
    x = batch[:, :46] # (batch_size, 46)
    y = batch[:, 46:] # (batch_size, 25)
    return x, y

# Evaluate loss
@torch.no_grad()
def estimate_loss():
    out = {}
    model.eval()
    for split in ['train', 'val']:
        losses = torch.zeros(eval_iters, device=device)
        for k in range(eval_iters):
            x, y = get_batch(split)
            index = torch.randint(25, (x.shape[0],1), device=device)
            # Add index to input
            new_x = torch.cat((x, index), dim=-1)
            # Only take the index we want to predict
            idx = index[0, 0]
            new_y = y[:, idx:idx+1]

            # Forward pass
            _, loss = m(new_x, new_y)
            losses[k] = loss.item()
        out[split] = losses.mean()
    model.train()
    return out

# Training
for epoch in range(epochs):
    # Evaluate model on train and test data
    if epoch % eval_interval == 0:
        loss = estimate_loss()
        print(f'step {epoch}: train loss {loss["train"]}, val loss {loss["val"]}')
        pass
    
    # Get random batch of data
    x, y = get_batch('train')

    # Perform forward pass for random index on output board
    index = torch.randint(25, (x.shape[0],1), device=device) / 25.
    # Add index to input
    new_x = torch.cat((x, index), dim=-1)
    # Only take the index we want to predict
    idx = int(index[0, 0] * 25)
    new_y = y[:, idx:idx+1]

    # Forward pass
    _, loss = m(new_x, new_y)
    optimizer.zero_grad(set_to_none=True)
    loss.backward()
    optimizer.step()

test = full_data[12].view(1, -1)
test_inp = test[:, :46]
test_out = test[:, 46:]

test_result = torch.tensor([[0]], device=device)
test_loss = torch.zeros(1, device=device)
for i in range(25):
    index = torch.tensor([[i]], device=device)
    new_test = torch.cat((test_inp, index), dim=-1)
    new_test_out = test_out[:, i:i+1]

    # Forward pass
    result, loss = m(new_test, new_test_out)
    print(result)
    res = result.argmax(dim=-1)
    test_result = torch.cat((test_result, res.view(-1, 1)), dim=-1)
    test_loss += loss
test_loss /= 25
    
print(f'test full board: {test_out}')
print(f'test result: {test_result}')
print(f'test loss: {test_loss.item()}')