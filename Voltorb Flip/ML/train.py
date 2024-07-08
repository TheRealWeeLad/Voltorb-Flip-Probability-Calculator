import torch
import torch.nn as nn
import torch.nn.functional as F

# hyperparameters
learning_rate = 1e-3
epochs = 10000
eval_interval = 1000
eval_iters = 200
batch_size = 64
device = 'cuda' if torch.cuda.is_available() else 'cpu'
# Training data columns: 0-level, 1:20-voltorb numbers, 21:45-known board state
# Add 1 to input for board index to predict
# What we want to predict: 46:70-full board state
input_sizes = [1, 20, 25, 1]
hidden_size = 512
output_size = 4

# Read training data
with open('training_data.csv', 'r') as f:
    data_text = f.read()

# Tokenize data
data_text = data_text.split('\n')
# final row is empty so we remove it
full_data = [list(map(lambda e: 0 if e == '' else float(e), data_row.split('\t'))) for data_row in data_text][:-1]

# Convert data to tensors
full_data = torch.tensor(full_data, dtype=torch.float32, device=device)

# MinMax normalization on each column of data
level = full_data[:, 0:1]
volt = full_data[:, 1:21]
known = full_data[:, 21:46]
full = full_data[:, 46:71]
level = (level - level.min()) / (level.max() - level.min())
volt = (volt - volt.min()) / (volt.max() - volt.min())
known = (known - known.min()) / (known.max() - known.min())
# Don't normalize predictions
# full = (full - full.min()) / (full.max() - full.min())
full_data = torch.cat((level, volt, known, full), dim=-1)

# Split data into train and test
n = int(0.9 * len(full_data))
training_data = full_data[:n]
test_data = full_data[n:]

# Model
class Model(nn.Module):
    def __init__(self):
        super().__init__()

        # Level interacts with Voltorb numbers
        self.level_layers = nn.Sequential(
            nn.Linear(input_sizes[0], 32),
        )
        # Voltorb nums interact with known board state
        self.voltorb_layers = nn.Sequential(
            nn.Linear(input_sizes[1], 64),
        )
        # Known board state interacts in mysterious ways :O
        self.known_layers = nn.Sequential(
            nn.Linear(input_sizes[2], 128),
        )

        self.hidden_layer = nn.Linear(32 + 64 + 128, hidden_size)
        # Output index interacts with final layer to output final predictions
        self.output_layer = nn.Linear(hidden_size + input_sizes[3], output_size)
        
        self.softmax = nn.Softmax(dim=-1)
    
    def forward(self, x, y):
        level = x[:, 0:1]
        voltorbs = x[:, 1:21]
        known = x[:, 21:46]
        index = x[:, 46:47]
        # Level
        level = self.level_layers(level)
        # Voltorb nums
        voltorbs = self.voltorb_layers(voltorbs)
        # Known board state
        known = self.known_layers(known)
        # Hidden layer
        x = self.hidden_layer(torch.cat((level, voltorbs, known), dim=-1))
        # Output layer
        x = self.output_layer(torch.cat((x, index), dim=-1))

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
        losses = torch.zeros(eval_iters)
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
    index = torch.randint(25, (x.shape[0],1), device=device)
    # Add index to input
    new_x = torch.cat((x, index), dim=-1)
    # Only take the index we want to predict
    idx = index[0, 0]
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